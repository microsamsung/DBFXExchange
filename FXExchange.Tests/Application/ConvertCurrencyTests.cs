
using System.Collections.Immutable;
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Application.Services;
using FXExchange.Application.Validation;
using FXExchange.Domain.Entities;
using FXExchange.Domain.Exceptions;
using FXExchange.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FXExchange.Tests.Application;

public sealed class CurrencyConverterTests
{
    private static ExchangeRateSnapshot CreateSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        var rates = new[]
        {
            new ExchangeRate("DKK", 100m, now),
            new ExchangeRate("EUR", 743.94m, now),
            new ExchangeRate("USD", 663.11m, now)
        }.ToImmutableDictionary(
            x => x.Currency,
            StringComparer.OrdinalIgnoreCase);

        return new ExchangeRateSnapshot(rates, 1, now);
    }

    private static CurrencyConverter CreateConverter(
        IExchangeRateProvider provider,
        IConvertCurrencyValidator validator,
        IConversionStrategyFactory factory)
    {
        return new CurrencyConverter(
            provider,
            validator,
            factory,
            NullLogger<CurrencyConverter>.Instance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validator_ValidAmount_Succeeds(int amount)
    {
        var validator = new ConvertCurrencyValidator();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            amount);

        validator.Validate(request, CreateSnapshot());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_NonPositiveAmount_Throws(int amount)
    {
        var validator = new ConvertCurrencyValidator();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            amount);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => validator.Validate(request, CreateSnapshot()));
    }

    [Theory]
    [InlineData("ABC/DKK")]
    [InlineData("EUR/XYZ")]
    [InlineData("ABC/ABC")]
    public void Validator_UnknownCurrency_Throws(string pair)
    {
        var validator = new ConvertCurrencyValidator();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse(pair),
            100m);

        Assert.Throws<UnknownCurrencyException>(
            () => validator.Validate(request, CreateSnapshot()));
    }

    [Fact]
    public void Validator_NullRequest_Throws()
    {
        var validator = new ConvertCurrencyValidator();

        Assert.Throws<ArgumentNullException>(
            () => validator.Validate(null!, CreateSnapshot()));
    }

    [Fact]
    public void Validator_NullSnapshot_Throws()
    {
        var validator = new ConvertCurrencyValidator();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        Assert.Throws<ArgumentNullException>(
            () => validator.Validate(request, null!));
    }

    [Fact]
    public async Task Converter_UsesSameSnapshotForValidationAndConversion()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var validator = new Mock<IConvertCurrencyValidator>();
        var factory = new Mock<IConversionStrategyFactory>();
        var strategy = new Mock<IConversionStrategy>();

        var snapshot = CreateSnapshot();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            10m);

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ExchangeRateSnapshot>(snapshot));

        factory
            .Setup(x => x.Create(request.Pair))
            .Returns(strategy.Object);

        strategy
            .Setup(x => x.Convert(
                request.Pair,
                request.Amount,
                snapshot))
            .Returns(74.394m);

        var converter = CreateConverter(
            provider.Object,
            validator.Object,
            factory.Object);

        // Act
        decimal result = await converter.ConvertAsync(request);

        // Assert
        Assert.Equal(74.394m, result);

        provider.Verify(
            x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        validator.Verify(
            x => x.Validate(request, snapshot),
            Times.Once);

        factory.Verify(
            x => x.Create(request.Pair),
            Times.Once);

        strategy.Verify(
            x => x.Convert(
                request.Pair,
                request.Amount,
                snapshot),
            Times.Once);
    }

    [Fact]
    public async Task Converter_ValidationFailure_DoesNotSelectStrategy()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var validator = new Mock<IConvertCurrencyValidator>();
        var factory = new Mock<IConversionStrategyFactory>();

        var snapshot = CreateSnapshot();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            -10m);

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ExchangeRateSnapshot>(snapshot));

        validator
            .Setup(x => x.Validate(request, snapshot))
            .Throws(new ArgumentOutOfRangeException());

        var converter = CreateConverter(
            provider.Object,
            validator.Object,
            factory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => converter.ConvertAsync(request));

        factory.Verify(
            x => x.Create(It.IsAny<CurrencyPair>()),
            Times.Never);
    }

    [Fact]
    public async Task Converter_NullRequest_DoesNotAccessProvider()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();

        var converter = CreateConverter(
            provider.Object,
            Mock.Of<IConvertCurrencyValidator>(),
            Mock.Of<IConversionStrategyFactory>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => converter.ConvertAsync(null!));

        provider.Verify(
            x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Converter_CancelledToken_ThrowsBeforeProviderCall()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();

        var converter = CreateConverter(
            provider.Object,
            Mock.Of<IConvertCurrencyValidator>(),
            Mock.Of<IConversionStrategyFactory>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => converter.ConvertAsync(request, cts.Token));

        provider.Verify(
            x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Converter_ProviderFailure_PropagatesException()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ExchangeRateSnapshot>(
                Task.FromException<ExchangeRateSnapshot>(
                    new InvalidOperationException(
                        "Rate provider unavailable."))));

        var converter = CreateConverter(
            provider.Object,
            Mock.Of<IConvertCurrencyValidator>(),
            Mock.Of<IConversionStrategyFactory>());

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => converter.ConvertAsync(request));

        Assert.Equal(
            "Rate provider unavailable.",
            exception.Message);
    }

    [Fact]
    public async Task Converter_StrategyFailure_PropagatesException()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var validator = new Mock<IConvertCurrencyValidator>();
        var factory = new Mock<IConversionStrategyFactory>();
        var strategy = new Mock<IConversionStrategy>();

        var snapshot = CreateSnapshot();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ExchangeRateSnapshot>(snapshot));

        factory
            .Setup(x => x.Create(request.Pair))
            .Returns(strategy.Object);

        strategy
            .Setup(x => x.Convert(
                request.Pair,
                request.Amount,
                snapshot))
            .Throws(new OverflowException());

        var converter = CreateConverter(
            provider.Object,
            validator.Object,
            factory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OverflowException>(
            () => converter.ConvertAsync(request));
    }

    [Fact]
    public async Task Converter_PassesCancellationTokenToProvider()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var validator = new Mock<IConvertCurrencyValidator>();
        var factory = new Mock<IConversionStrategyFactory>();
        var strategy = new Mock<IConversionStrategy>();

        using var cts = new CancellationTokenSource();

        var snapshot = CreateSnapshot();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        provider
            .Setup(x => x.GetSnapshotAsync(cts.Token))
            .Returns(new ValueTask<ExchangeRateSnapshot>(snapshot));

        factory
            .Setup(x => x.Create(request.Pair))
            .Returns(strategy.Object);

        strategy
            .Setup(x => x.Convert(
                request.Pair,
                request.Amount,
                snapshot))
            .Returns(743.94m);

        var converter = CreateConverter(
            provider.Object,
            validator.Object,
            factory.Object);

        // Act
        decimal result = await converter.ConvertAsync(
            request,
            cts.Token);

        // Assert
        Assert.Equal(743.94m, result);

        provider.Verify(
            x => x.GetSnapshotAsync(cts.Token),
            Times.Once);
    }
}

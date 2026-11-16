
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Console.Constants;
using FXExchange.Console.ExceptionHandling;
using FXExchange.Console.Services;
using FXExchange.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FXExchange.Tests.Console;

public sealed class ConsoleRunnerTests
{
    private readonly ConsoleExceptionHandler _exceptionHandler =
        new(NullLogger<ConsoleExceptionHandler>.Instance);

    private ConsoleRunner CreateRunner(
        Mock<ICurrencyConverter> converter)
    {
        return new ConsoleRunner(
            converter.Object,
            _exceptionHandler,
            NullLogger<ConsoleRunner>.Instance);
    }

    // -----------------------------------------
    // Exception handling tests
    // -----------------------------------------

    [Fact]
    public void ArgumentException_ReturnsValidationCode()
    {
        int result = _exceptionHandler.Handle(
            new ArgumentException("Invalid input"));

        Assert.Equal(
            ConsoleConstants.ValidationExitCode,
            result);
    }

    [Fact]
    public void UnknownCurrency_ReturnsValidationCode()
    {
        int result = _exceptionHandler.Handle(
            new UnknownCurrencyException("ABC"));

        Assert.Equal(
            ConsoleConstants.ValidationExitCode,
            result);
    }

    [Fact]
    public void Overflow_ReturnsValidationCode()
    {
        int result = _exceptionHandler.Handle(
            new OverflowException());

        Assert.Equal(
            ConsoleConstants.ValidationExitCode,
            result);
    }

    [Fact]
    public void UnexpectedException_ReturnsErrorCode()
    {
        int result = _exceptionHandler.Handle(
            new InvalidOperationException("Unexpected failure"));

        Assert.Equal(
            ConsoleConstants.ErrorExitCode,
            result);
    }

    [Fact]
    public void NullException_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _exceptionHandler.Handle(null!));
    }

    // -----------------------------------------
    // Currency converter tests
    // -----------------------------------------

    [Fact]
    public async Task RunAsync_ValidArguments_ReturnsSuccess()
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();

        converter
            .Setup(x => x.ConvertAsync(
                It.IsAny<CurrencyConversionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(743.94m);

        var runner = CreateRunner(converter);

        // Act
        int result = await runner.RunAsync(
            new[] { "EUR/DKK", "100" });

        // Assert
        Assert.Equal(
            ConsoleConstants.SuccessExitCode,
            result);

        converter.Verify(
            x => x.ConvertAsync(
                It.Is<CurrencyConversionRequest>(r =>
                    r.Pair.Source == "EUR" &&
                    r.Pair.Target == "DKK" &&
                    r.Amount == 100m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("INVALID", "100")]
    [InlineData("EUR/DKK", "abc")]
    [InlineData("EUR/DKK", "10,20")]
    public async Task RunAsync_InvalidInput_ReturnsValidationCode(
        string pair,
        string amount)
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();
        var runner = CreateRunner(converter);

        // Act
        int result = await runner.RunAsync(
            new[] { pair, amount });

        // Assert
        Assert.Equal(
            ConsoleConstants.ValidationExitCode,
            result);

        converter.Verify(
            x => x.ConvertAsync(
                It.IsAny<CurrencyConversionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_MissingArgument_ReturnsValidationCode()
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();
        var runner = CreateRunner(converter);

        // Act
        int result = await runner.RunAsync(
            new[] { "EUR/DKK" });

        // Assert
        Assert.Equal(
            ConsoleConstants.ValidationExitCode,
            result);

        converter.Verify(
            x => x.ConvertAsync(
                It.IsAny<CurrencyConversionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ConverterValidationFailure_ReturnsValidationCode()
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();

        converter
            .Setup(x => x.ConvertAsync(
                It.IsAny<CurrencyConversionRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ArgumentOutOfRangeException("amount"));

        var runner = CreateRunner(converter);

        // Act
        int result = await runner.RunAsync(
            new[] { "EUR/DKK", "-100" });

        // Assert
        Assert.Equal(
            ConsoleConstants.ValidationExitCode,
            result);
    }

    [Fact]
    public async Task RunAsync_ConverterUnexpectedFailure_ReturnsErrorCode()
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();

        converter
            .Setup(x => x.ConvertAsync(
                It.IsAny<CurrencyConversionRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException("Service unavailable"));

        var runner = CreateRunner(converter);

        // Act
        int result = await runner.RunAsync(
            new[] { "EUR/DKK", "100" });

        // Assert
        Assert.Equal(
            ConsoleConstants.ErrorExitCode,
            result);
    }

    [Fact]
    public async Task RunAsync_CancelledToken_ReturnsErrorCode()
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();
        var runner = CreateRunner(converter);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        int result = await runner.RunAsync(
            new[] { "EUR/DKK", "100" },
            cts.Token);

        // Assert
        Assert.Equal(
            ConsoleConstants.ErrorExitCode,
            result);

        converter.Verify(
            x => x.ConvertAsync(
                It.IsAny<CurrencyConversionRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_NullArguments_Throws()
    {
        // Arrange
        var converter = new Mock<ICurrencyConverter>();
        var runner = CreateRunner(converter);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => runner.RunAsync(null!));
    }
}

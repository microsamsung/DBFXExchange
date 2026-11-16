
using System;
using System.Collections.Immutable;
using System.Linq;
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Domain.Entities;
using FXExchange.Domain.Exceptions;
using FXExchange.Domain.ValueObjects;
using FXExchange.Infrastructure.Factories;
using FXExchange.Infrastructure.Strategies;
using Xunit;

namespace FXExchange.Tests.Infrastructure;

public sealed class StrategyFactoryTests
{
    private static ExchangeRateSnapshot CreateSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        var rates = new[]
        {
            new ExchangeRate("DKK", 100m, now),
            new ExchangeRate("EUR", 743.94m, now),
            new ExchangeRate("USD", 663.11m, now),
            new ExchangeRate("JPY", 5.9740m, now)
        }.ToImmutableDictionary(
            x => x.Currency,
            StringComparer.OrdinalIgnoreCase);

        return new ExchangeRateSnapshot(
            rates,
            1,
            now);
    }

    private static ConversionStrategyFactory CreateFactory()
    {
        return new ConversionStrategyFactory(
            new IConversionStrategy[]
            {
                new IdentityConversionStrategy(),
                new CrossRateConversionStrategy()
            });
    }

    // =========================================
    // Factory Tests
    // =========================================

    [Theory]
    [InlineData("EUR/EUR")]
    [InlineData("DKK/DKK")]
    [InlineData("USD/USD")]
    [InlineData("JPY/JPY")]
    public void SameCurrency_SelectsIdentityStrategy(
        string currencyPair)
    {
        // Arrange
        var factory = CreateFactory();

        var pair = CurrencyPair.Parse(currencyPair);

        // Act
        var strategy = factory.Create(pair);

        // Assert
        Assert.IsType<IdentityConversionStrategy>(strategy);
    }

    [Theory]
    [InlineData("EUR/DKK")]
    [InlineData("DKK/EUR")]
    [InlineData("EUR/USD")]
    [InlineData("JPY/DKK")]
    public void DifferentCurrency_SelectsCrossRateStrategy(
        string currencyPair)
    {
        // Arrange
        var factory = CreateFactory();

        var pair = CurrencyPair.Parse(currencyPair);

        // Act
        var strategy = factory.Create(pair);

        // Assert
        Assert.IsType<CrossRateConversionStrategy>(strategy);
    }

    [Fact]
    public void EmptyStrategies_ThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new ConversionStrategyFactory(
                Array.Empty<IConversionStrategy>()));
    }

    [Fact]
    public void DuplicateStrategies_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new ConversionStrategyFactory(
            new IConversionStrategy[]
            {
                new CrossRateConversionStrategy(),
                new CrossRateConversionStrategy()
            });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(
                CurrencyPair.Parse("EUR/DKK")));
    }

    [Fact]
    public void MissingMatchingStrategy_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new ConversionStrategyFactory(
            new IConversionStrategy[]
            {
                new IdentityConversionStrategy()
            });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(
                CurrencyPair.Parse("EUR/DKK")));
    }

    // =========================================
    // Currency Conversion Test Data
    // =========================================

    public static TheoryData<string, decimal, decimal>
        ConversionCases => new()
    {
        { "EUR/DKK", 1m, 7.4394m },
        { "EUR/DKK", 100m, 743.94m },
        { "USD/DKK", 100m, 663.11m },
        { "DKK/EUR", 743.94m, 100m },
        { "EUR/USD", 663.11m, 743.94m },
        { "JPY/DKK", 100m, 5.974m },
        { "DKK/DKK", 100m, 100m }
    };

    // =========================================
    // Cross Rate Strategy Tests
    // =========================================

    [Theory]
    [MemberData(nameof(ConversionCases))]
    public void Conversion_ReturnsExpectedAmount(
        string currencyPair,
        decimal amount,
        decimal expected)
    {
        // Arrange
        var factory = CreateFactory();

        var pair = CurrencyPair.Parse(currencyPair);

        var snapshot = CreateSnapshot();

        var strategy = factory.Create(pair);

        // Act
        decimal result = strategy.Convert(
            pair,
            amount,
            snapshot);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CrossRate_UsesCorrectConversionFormula()
    {
        // Arrange
        var strategy = new CrossRateConversionStrategy();

        var snapshot = CreateSnapshot();

        var pair = CurrencyPair.Parse("EUR/USD");

        decimal amount = 100m;

        // Rate quotation: DKK per 100 units.
        // EUR = 743.94
        // USD = 663.11
        //
        // Converted amount:
        // 100 * 743.94 / 663.11

        decimal expected =
            amount * 743.94m / 663.11m;

        // Act
        decimal result = strategy.Convert(
            pair,
            amount,
            snapshot);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CrossRate_ReverseConversion_IsConsistent()
    {
        // Arrange
        var strategy = new CrossRateConversionStrategy();

        var snapshot = CreateSnapshot();

        decimal originalAmount = 100m;

        // Act
        decimal converted = strategy.Convert(
            CurrencyPair.Parse("EUR/USD"),
            originalAmount,
            snapshot);

        decimal reversed = strategy.Convert(
            CurrencyPair.Parse("USD/EUR"),
            converted,
            snapshot);

        // Assert
        // Allow a very small decimal rounding difference.
        Assert.InRange(
            reversed,
            originalAmount - 0.000001m,
            originalAmount + 0.000001m);
    }

    [Fact]
    public void CrossRate_ZeroAmount_ReturnsZero()
    {
        // Arrange
        var strategy = new CrossRateConversionStrategy();

        // Act
        decimal result = strategy.Convert(
            CurrencyPair.Parse("EUR/DKK"),
            0m,
            CreateSnapshot());

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CrossRate_UnknownSourceCurrency_Throws()
    {
        // Arrange
        var strategy = new CrossRateConversionStrategy();

        // Act & Assert
        Assert.Throws<UnknownCurrencyException>(() =>
            strategy.Convert(
                CurrencyPair.Parse("ABC/DKK"),
                100m,
                CreateSnapshot()));
    }

    [Fact]
    public void CrossRate_UnknownTargetCurrency_Throws()
    {
        // Arrange
        var strategy = new CrossRateConversionStrategy();

        // Act & Assert
        Assert.Throws<UnknownCurrencyException>(() =>
            strategy.Convert(
                CurrencyPair.Parse("EUR/ABC"),
                100m,
                CreateSnapshot()));
    }

    [Fact]
    public void CrossRate_Overflow_Throws()
    {
        // Arrange
        var strategy = new CrossRateConversionStrategy();

        // Act & Assert
        Assert.Throws<OverflowException>(() =>
            strategy.Convert(
                CurrencyPair.Parse("EUR/JPY"),
                decimal.MaxValue,
                CreateSnapshot()));
    }

    // =========================================
    // Identity Strategy Tests
    // =========================================

    [Fact]
    public void Identity_ReturnsOriginalAmount()
    {
        // Arrange
        var strategy = new IdentityConversionStrategy();

        // Act
        decimal result = strategy.Convert(
            CurrencyPair.Parse("EUR/EUR"),
            125.50m,
            CreateSnapshot());

        // Assert
        Assert.Equal(125.50m, result);
    }

    [Fact]
    public void Identity_ZeroAmount_ReturnsZero()
    {
        // Arrange
        var strategy = new IdentityConversionStrategy();

        // Act
        decimal result = strategy.Convert(
            CurrencyPair.Parse("DKK/DKK"),
            0m,
            CreateSnapshot());

        // Assert
        Assert.Equal(0m, result);
    }

    [Fact]
    public void Identity_UnknownCurrency_Throws()
    {
        // Arrange
        var strategy = new IdentityConversionStrategy();

        // Act & Assert
        Assert.Throws<UnknownCurrencyException>(() =>
            strategy.Convert(
                CurrencyPair.Parse("ABC/ABC"),
                100m,
                CreateSnapshot()));
    }
}

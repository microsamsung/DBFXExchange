
using FXExchange.Domain.ValueObjects;
using Xunit;

namespace FXExchange.Tests.Domain;

public sealed class CurrencyPairTests
{
    [Theory]
    [InlineData("EUR/DKK", "EUR", "DKK")]
    [InlineData("eur/usd", "EUR", "USD")]
    [InlineData(" GBP/CHF ", "GBP", "CHF")]
    [InlineData("JPY/JPY", "JPY", "JPY")]
    [InlineData("dkk/eur", "DKK", "EUR")]
    [InlineData("Usd/Gbp", "USD", "GBP")]
    [InlineData("eur / dkk", "EUR", "DKK")]
    [InlineData("EUR/ DKK", "EUR", "DKK")]
    [InlineData(" EUR /DKK ", "EUR", "DKK")]
    [InlineData(" eur  /  usd ", "EUR", "USD")]
    [InlineData(" EUR / DKK ", "EUR", "DKK")]
    public void Parse_ValidInput_ReturnsNormalizedPair(
        string input,
        string source,
        string target)
    {
        // Act
        CurrencyPair result = CurrencyPair.Parse(input);

        // Assert
        Assert.Equal(source, result.Source);
        Assert.Equal(target, result.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("EUR")]
    [InlineData("EUR-DKK")]
    [InlineData("EUR/DKK/USD")]
    [InlineData("EU/DKK")]
    [InlineData("EUR/DK")]
    [InlineData("123/DKK")]
    [InlineData("EUR/12A")]
    [InlineData("/DKK")]
    [InlineData("EUR/")]
    [InlineData("EUR//DKK")]
    [InlineData("EUR DKK")]
    [InlineData("EURO/DKK")]
    [InlineData("EUR/DKK1")]
    [InlineData("EUR\\DKK")]
    [InlineData("EU R/DKK")]
    [InlineData("EUR/D KK")]
    [InlineData("E UR / DKK")]
    public void Parse_InvalidInput_Throws(string? input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => CurrencyPair.Parse(input));
    }

    [Fact]
    public void Parse_EquivalentPairs_HaveValueEquality()
    {
        // Arrange
        var first = CurrencyPair.Parse("EUR/DKK");
        var second = CurrencyPair.Parse("eur / dkk");

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void Parse_EquivalentPairs_HaveSameHashCode()
    {
        // Arrange
        var first = CurrencyPair.Parse("EUR/DKK");
        var second = CurrencyPair.Parse(" eur / dkk ");

        // Assert
        Assert.Equal(
            first.GetHashCode(),
            second.GetHashCode());
    }

    [Fact]
    public void Parse_DifferentPairs_AreNotEqual()
    {
        // Arrange
        var first = CurrencyPair.Parse("EUR/DKK");
        var second = CurrencyPair.Parse("USD/DKK");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Parse_ReversedPairs_AreNotEqual()
    {
        // Arrange
        var first = CurrencyPair.Parse("EUR/DKK");
        var second = CurrencyPair.Parse("DKK/EUR");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Parse_SameSourceAndTarget_IsValid()
    {
        // Act
        var pair = CurrencyPair.Parse("eur / eur");

        // Assert
        Assert.Equal("EUR", pair.Source);
        Assert.Equal("EUR", pair.Target);
    }

    [Fact]
    public void ToString_ReturnsNormalizedPair()
    {
        // Arrange
        var pair = CurrencyPair.Parse(" eur / dkk ");

        // Act
        string result = pair.ToString();

        // Assert
        Assert.Equal("EUR/DKK", result);
    }
}

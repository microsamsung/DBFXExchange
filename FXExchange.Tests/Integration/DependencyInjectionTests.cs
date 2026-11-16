
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Domain.ValueObjects;
using FXExchange.Infrastructure;
using FXExchange.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FXExchange.Tests.Integration;

public sealed class DependencyInjectionTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        // Register logging.
        services.AddLogging();

        // Configure initial exchange rates.
        services.Configure<ExchangeRateOptions>(options =>
        {
            options.DkkPerHundred = new Dictionary<string, decimal>
            {
                ["DKK"] = 100m,
                ["EUR"] = 743.94m,
                ["USD"] = 663.11m
            };
        });

        // Disable automatic rate simulation during tests.
        services.Configure<RateSimulationOptions>(options =>
        {
            options.Enabled = false;
        });

        // Register application and infrastructure services.
        services.AddFxExchange();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    [Fact]
    public void AllApplicationServices_Resolve()
    {
        // Arrange
        using var provider = CreateProvider();

        // Act & Assert
        Assert.NotNull(
            provider.GetRequiredService<IExchangeRateProvider>());

        Assert.NotNull(
            provider.GetRequiredService<IExchangeRateUpdater>());

        Assert.NotNull(
            provider.GetRequiredService<IConvertCurrencyValidator>());

        Assert.NotNull(
            provider.GetRequiredService<IConversionStrategyFactory>());

        Assert.NotNull(
            provider.GetRequiredService<ICurrencyConverter>());
    }

    [Fact]
    public async Task Converter_ConvertsUsingRegisteredServices()
    {
        // Arrange
        using var provider = CreateProvider();

        var converter = provider.GetRequiredService<
            ICurrencyConverter>();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            1m);

        // Act
        decimal result = await converter.ConvertAsync(request);

        // Assert
        Assert.Equal(7.4394m, result);
    }

    [Fact]
    public async Task ProviderAndUpdater_ShareSameSingleton()
    {
        // Arrange
        using var provider = CreateProvider();

        var reader = provider.GetRequiredService<
            IExchangeRateProvider>();

        var updater = provider.GetRequiredService<
            IExchangeRateUpdater>();

        // Assert: both interfaces resolve to the same instance.
        Assert.Same(reader, updater);

        // Act
        await updater.UpdateRateAsync("EUR", 750m);

        var snapshot = await reader.GetSnapshotAsync();

        // Assert
        Assert.Equal(750m, snapshot.GetRate("EUR"));
        Assert.Equal(2L, snapshot.Version);
    }

    [Fact]
    public async Task RateUpdate_AffectsSubsequentConversions()
    {
        // Arrange
        using var provider = CreateProvider();

        var converter = provider.GetRequiredService<
            ICurrencyConverter>();

        var updater = provider.GetRequiredService<
            IExchangeRateUpdater>();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        // Act
        decimal before = await converter.ConvertAsync(request);

        await updater.UpdateRateAsync("EUR", 750m);

        decimal after = await converter.ConvertAsync(request);

        // Assert
        Assert.Equal(743.94m, before);
        Assert.Equal(750m, after);
    }

    [Fact]
    public async Task MultipleConverters_UseSharedExchangeRateProvider()
    {
        // Arrange
        using var provider = CreateProvider();

        var converter1 = provider.GetRequiredService<
            ICurrencyConverter>();

        var converter2 = provider.GetRequiredService<
            ICurrencyConverter>();

        var updater = provider.GetRequiredService<
            IExchangeRateUpdater>();

        // Act
        await updater.UpdateRateAsync("EUR", 760m);

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        decimal result1 = await converter1.ConvertAsync(request);
        decimal result2 = await converter2.ConvertAsync(request);

        // Assert
        Assert.Equal(760m, result1);
        Assert.Equal(760m, result2);
    }

    [Fact]
    public async Task ProviderSnapshot_RemainsConsistentAfterUpdate()
    {
        // Arrange
        using var provider = CreateProvider();

        var reader = provider.GetRequiredService<
            IExchangeRateProvider>();

        var updater = provider.GetRequiredService<
            IExchangeRateUpdater>();

        var oldSnapshot = await reader.GetSnapshotAsync();

        // Act
        await updater.UpdateRateAsync("EUR", 750m);

        var newSnapshot = await reader.GetSnapshotAsync();

        // Assert
        Assert.Equal(743.94m, oldSnapshot.GetRate("EUR"));
        Assert.Equal(750m, newSnapshot.GetRate("EUR"));

        Assert.Equal(1L, oldSnapshot.Version);
        Assert.Equal(2L, newSnapshot.Version);

        Assert.NotSame(oldSnapshot, newSnapshot);
    }

    [Fact]
    public async Task Converter_CancelledRequest_Throws()
    {
        // Arrange
        using var provider = CreateProvider();

        var converter = provider.GetRequiredService<
            ICurrencyConverter>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new CurrencyConversionRequest(
            CurrencyPair.Parse("EUR/DKK"),
            100m);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => converter.ConvertAsync(request, cts.Token));
    }
}


using FXExchange.Domain.Exceptions;
using FXExchange.Infrastructure.Configuration;
using FXExchange.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FXExchange.Tests.Infrastructure;

public sealed class ExchangeRateProviderTests
{
    private static ConcurrentExchangeRateProvider CreateProvider(
        Dictionary<string, decimal>? rates = null)
    {
        var options = Options.Create(new ExchangeRateOptions
        {
            DkkPerHundred = rates ?? new Dictionary<string, decimal>
            {
                ["DKK"] = 100m,
                ["EUR"] = 743.94m,
                ["USD"] = 663.11m
            }
        });

        return new ConcurrentExchangeRateProvider(
            options,
            NullLogger<ConcurrentExchangeRateProvider>.Instance);
    }

    [Fact]
    public async Task Constructor_LoadsInitialRates()
    {
        var provider = CreateProvider();

        var snapshot = await provider.GetSnapshotAsync();

        Assert.Equal(743.94m, snapshot.GetRate("EUR"));
        Assert.Equal(100m, snapshot.GetRate("DKK"));
        Assert.Equal(1L, snapshot.Version);
    }

    [Fact]
    public async Task UpdateRateAsync_PublishesNewVersion()
    {
        var provider = CreateProvider();

        await provider.UpdateRateAsync("EUR", 750m);

        var snapshot = await provider.GetSnapshotAsync();

        Assert.Equal(750m, snapshot.GetRate("EUR"));
        Assert.Equal(2L, snapshot.Version);
    }

    [Fact]
    public async Task UpdateRateAsync_PreservesOldSnapshot()
    {
        var provider = CreateProvider();

        var oldSnapshot = await provider.GetSnapshotAsync();

        await provider.UpdateRateAsync("EUR", 750m);

        var newSnapshot = await provider.GetSnapshotAsync();

        Assert.Equal(743.94m, oldSnapshot.GetRate("EUR"));
        Assert.Equal(750m, newSnapshot.GetRate("EUR"));

        Assert.NotSame(oldSnapshot, newSnapshot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateRateAsync_InvalidRate_Throws(int rate)
    {
        var provider = CreateProvider();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () =>
                await provider.UpdateRateAsync("EUR", rate));
    }

    [Fact]
    public async Task UpdateRateAsync_UnknownCurrency_Throws()
    {
        var provider = CreateProvider();

        await Assert.ThrowsAsync<UnknownCurrencyException>(
            async () =>
                await provider.UpdateRateAsync("ABC", 100m));
    }

    [Fact]
    public async Task UpdateRateAsync_Dkk_Throws()
    {
        var provider = CreateProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await provider.UpdateRateAsync("DKK", 101m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("EU")]
    [InlineData("123")]
    public async Task UpdateRateAsync_InvalidCode_Throws(
        string code)
    {
        var provider = CreateProvider();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () =>
                await provider.UpdateRateAsync(code, 100m));
    }

    [Fact]
    public void Constructor_MissingBaseCurrency_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => CreateProvider(new Dictionary<string, decimal>
            {
                ["EUR"] = 743.94m
            }));
    }

    [Fact]
    public void Constructor_InvalidBaseRate_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => CreateProvider(new Dictionary<string, decimal>
            {
                ["DKK"] = 99m,
                ["EUR"] = 743.94m
            }));
    }

    [Fact]
    public void Constructor_NegativeRate_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => CreateProvider(new Dictionary<string, decimal>
            {
                ["DKK"] = 100m,
                ["EUR"] = -1m
            }));
    }

    [Fact]
    public async Task ParallelUpdates_PreserveVersionCount()
    {
        var provider = CreateProvider();

        const int count = 100;

        // Concurrent calls exercise the provider's internal lock.
        var tasks = Enumerable.Range(1, count)
            .Select(i => Task.Run(async () =>
            {
                await provider.UpdateRateAsync(
                    "EUR",
                    700m + i);
            }));

        await Task.WhenAll(tasks);

        var snapshot = await provider.GetSnapshotAsync();

        Assert.Equal(count + 1L, snapshot.Version);

        Assert.InRange(
            snapshot.GetRate("EUR"),
            701m,
            800m);
    }

    [Fact]
    public async Task ParallelReadersAndWriters_SeeValidSnapshots()
    {
        var provider = CreateProvider();

        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0)
                {
                    await provider.UpdateRateAsync(
                        "EUR",
                        700m + i);
                }
                else
                {
                    var snapshot =
                        await provider.GetSnapshotAsync();

                    Assert.True(
                        snapshot.GetRate("EUR") > 0);

                    Assert.Equal(
                        100m,
                        snapshot.GetRate("DKK"));
                }
            }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task GetSnapshotAsync_CancelledToken_Throws()
    {
        var provider = CreateProvider();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await provider.GetSnapshotAsync(cts.Token));
    }

    [Fact]
    public async Task UpdateRateAsync_CancelledToken_DoesNotUpdate()
    {
        var provider = CreateProvider();

        var original = await provider.GetSnapshotAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await provider.UpdateRateAsync(
                    "EUR",
                    750m,
                    cts.Token));

        var current = await provider.GetSnapshotAsync();

        Assert.Equal(
            original.Version,
            current.Version);

        Assert.Equal(
            original.GetRate("EUR"),
            current.GetRate("EUR"));
    }

    [Fact]
    public async Task UpdateRateAsync_SequentialUpdates_IncrementVersion()
    {
        var provider = CreateProvider();

        await provider.UpdateRateAsync("EUR", 750m);
        await provider.UpdateRateAsync("USD", 670m);
        await provider.UpdateRateAsync("EUR", 760m);

        var snapshot = await provider.GetSnapshotAsync();

        Assert.Equal(4L, snapshot.Version);
        Assert.Equal(760m, snapshot.GetRate("EUR"));
        Assert.Equal(670m, snapshot.GetRate("USD"));
        Assert.Equal(100m, snapshot.GetRate("DKK"));
    }

    [Fact]
    public async Task UpdateRateAsync_NormalizesCurrencyCode()
    {
        var provider = CreateProvider();

        await provider.UpdateRateAsync(" eur ", 750m);

        var snapshot = await provider.GetSnapshotAsync();

        Assert.Equal(750m, snapshot.GetRate("EUR"));
        Assert.Equal(2L, snapshot.Version);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithoutUpdates_ReturnsSameInstance()
    {
        var provider = CreateProvider();

        var first = await provider.GetSnapshotAsync();
        var second = await provider.GetSnapshotAsync();

        Assert.Same(first, second);
    }
}

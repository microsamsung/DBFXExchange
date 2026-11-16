
using System.Collections.Immutable;
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Domain.Entities;
using FXExchange.Infrastructure.BackgroundServices;
using FXExchange.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FXExchange.Tests.Infrastructure;

public sealed class BackgroundServiceTests
{
    private static ExchangeRateSnapshot CreateSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        var rates = new[]
        {
            new ExchangeRate("DKK", 100m, now),
            new ExchangeRate("EUR", 743.94m, now)
        }.ToImmutableDictionary(
            x => x.Currency,
            StringComparer.OrdinalIgnoreCase);

        return new ExchangeRateSnapshot(rates, 1, now);
    }

    private static ExchangeRateBackgroundService CreateWorker(
        IExchangeRateProvider provider,
        IExchangeRateUpdater updater,
        bool enabled,
        int intervalSeconds = 1,
        decimal maxChangePercent = 0.5m)
    {
        return new ExchangeRateBackgroundService(
            provider,
            updater,
            Options.Create(new RateSimulationOptions
            {
                Enabled = enabled,
                IntervalSeconds = intervalSeconds,
                MaxChangePercent = maxChangePercent
            }),
            NullLogger<ExchangeRateBackgroundService>.Instance);
    }

    [Fact]
    public async Task DisabledWorker_DoesNotUpdateRates()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var updater = new Mock<IExchangeRateUpdater>();

        using var worker = CreateWorker(
            provider.Object,
            updater.Object,
            enabled: false);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        provider.Verify(
            x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        updater.Verify(
            x => x.UpdateRateAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnabledWorker_UpdatesCurrency()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var updater = new Mock<IExchangeRateUpdater>();

        var snapshot = CreateSnapshot();

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ExchangeRateSnapshot>(snapshot));

        var updated = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        updater
            .Setup(x => x.UpdateRateAsync(
                "EUR",
                It.Is<decimal>(rate => rate > 0),
                It.IsAny<CancellationToken>()))
            .Callback<string, decimal, CancellationToken>(
                (currency, rate, token) =>
                {
                    updated.TrySetResult(true);
                })
            .Returns(ValueTask.CompletedTask);

        using var worker = CreateWorker(
            provider.Object,
            updater.Object,
            enabled: true,
            intervalSeconds: 1,
            maxChangePercent: 0.5m);

        // Act
        await worker.StartAsync(CancellationToken.None);

        try
        {
            await updated.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

            // Assert
            provider.Verify(
                x => x.GetSnapshotAsync(
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);

            updater.Verify(
                x => x.UpdateRateAsync(
                    "EUR",
                    It.Is<decimal>(rate => rate > 0),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Worker_CanStopBeforeFirstUpdate()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var updater = new Mock<IExchangeRateUpdater>();

        using var worker = CreateWorker(
            provider.Object,
            updater.Object,
            enabled: true,
            intervalSeconds: 60);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        updater.Verify(
            x => x.UpdateRateAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnabledWorker_DoesNotUpdateBaseCurrency()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var updater = new Mock<IExchangeRateUpdater>();

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ExchangeRateSnapshot>(
                CreateSnapshot()));

        var updated = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        updater
            .Setup(x => x.UpdateRateAsync(
                "EUR",
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, decimal, CancellationToken>(
                (_, _, _) => updated.TrySetResult(true))
            .Returns(ValueTask.CompletedTask);

        using var worker = CreateWorker(
            provider.Object,
            updater.Object,
            enabled: true);

        await worker.StartAsync(CancellationToken.None);

        try
        {
            await updated.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert
        updater.Verify(
            x => x.UpdateRateAsync(
                "DKK",
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnabledWorker_OnlyBaseCurrency_DoesNotUpdate()
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var updater = new Mock<IExchangeRateUpdater>();

        var now = DateTimeOffset.UtcNow;

        var rates = new[]
        {
            new ExchangeRate("DKK", 100m, now)
        }.ToImmutableDictionary(
            x => x.Currency,
            StringComparer.OrdinalIgnoreCase);

        var snapshot = new ExchangeRateSnapshot(rates, 1, now);

        var snapshotRead = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        provider
            .Setup(x => x.GetSnapshotAsync(
                It.IsAny<CancellationToken>()))
            .Callback(() => snapshotRead.TrySetResult(true))
            .Returns(new ValueTask<ExchangeRateSnapshot>(snapshot));

        using var worker = CreateWorker(
            provider.Object,
            updater.Object,
            enabled: true);

        await worker.StartAsync(CancellationToken.None);

        try
        {
            await snapshotRead.Task.WaitAsync(
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert
        updater.Verify(
            x => x.UpdateRateAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(-1, 0.5)]
    [InlineData(1, -0.1)]
    [InlineData(1, 100)]
    public async Task InvalidConfiguration_StopsWorker(
        int intervalSeconds,
        double maxChangePercent)
    {
        // Arrange
        var provider = new Mock<IExchangeRateProvider>();
        var updater = new Mock<IExchangeRateUpdater>();

        using var worker = CreateWorker(
            provider.Object,
            updater.Object,
            enabled: true,
            intervalSeconds: intervalSeconds,
            maxChangePercent: (decimal)maxChangePercent);

        // Act & Assert
        // BackgroundService startup behavior differs depending on
        // whether ExecuteAsync has completed when StartAsync checks it.
        // Observe the executing task rather than assuming StartAsync
        // always throws.
        try
        {
            await worker.StartAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        try
        {
            await Task.Delay(100);

            Assert.NotNull(worker.ExecuteTask);
            Assert.True(worker.ExecuteTask.IsFaulted);

            Assert.IsType<InvalidOperationException>(
                worker.ExecuteTask.Exception?.GetBaseException());
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}

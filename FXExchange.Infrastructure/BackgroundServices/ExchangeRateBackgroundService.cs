
using FXExchange.Application.Abstractions;
using FXExchange.Domain.Constants;
using FXExchange.Infrastructure.Configuration;
using FXExchange.Infrastructure.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FXExchange.Infrastructure.BackgroundServices;

public sealed class ExchangeRateBackgroundService : BackgroundService
{
    private readonly IExchangeRateProvider _provider;
    private readonly IExchangeRateUpdater _updater;
    private readonly RateSimulationOptions _options;
    private readonly ILogger<ExchangeRateBackgroundService> _logger;

    public ExchangeRateBackgroundService(
        IExchangeRateProvider provider,
        IExchangeRateUpdater updater,
        IOptions<RateSimulationOptions> options,
        ILogger<ExchangeRateBackgroundService> logger)
    {
        _provider = provider ??
            throw new ArgumentNullException(nameof(provider));

        _updater = updater ??
            throw new ArgumentNullException(nameof(updater));

        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Rate simulation is disabled.");
            return;
        }

        ValidateOptions();

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.IntervalSeconds));

        _logger.LogInformation(
            "FX background service started. Interval={Seconds}s",
            _options.IntervalSeconds);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await UpdateRandomRateAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Simulated rate update rejected.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected simulated rate update failure.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "FX background service stopped by cancellation.");
        }
    }

    private async Task UpdateRandomRateAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await _provider.GetSnapshotAsync(
            cancellationToken);

        var currencies = snapshot.Currencies
            .Where(x => x != CurrencyConstants.BaseCurrency)
            .ToArray();

        if (currencies.Length == 0)
            return;

        string currency =
            currencies[Random.Shared.Next(currencies.Length)];

        decimal oldRate = snapshot.GetRate(currency);

        decimal changePercent =
            ((decimal)Random.Shared.NextDouble() * 2m - 1m)
            * _options.MaxChangePercent;

        decimal multiplier =
            1m + changePercent /
            InfrastructureConstants.PercentageDivisor;

        decimal newRate = Math.Round(
            oldRate * multiplier,
            InfrastructureConstants.RateDecimalPlaces,
            MidpointRounding.ToEven);

        if (newRate <= 0)
        {
            throw new InvalidOperationException(
                "Simulated exchange rate must remain positive.");
        }

        await _updater.UpdateRateAsync(
            currency,
            newRate,
            cancellationToken);
    }

    private void ValidateOptions()
    {
        if (_options.IntervalSeconds <= 0 ||
            _options.MaxChangePercent < 0 ||
            _options.MaxChangePercent >= 100)
        {
            throw new InvalidOperationException(
                "Invalid rate simulation configuration.");
        }
    }
}

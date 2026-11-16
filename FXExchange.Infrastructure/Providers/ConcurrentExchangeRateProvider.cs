
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Domain.Constants;
using FXExchange.Domain.Entities;
using FXExchange.Domain.Exceptions;
using FXExchange.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FXExchange.Infrastructure.Providers;

public sealed class ConcurrentExchangeRateProvider :
    IExchangeRateProvider, IExchangeRateUpdater
{
    private readonly ConcurrentDictionary<string, ExchangeRate> _rates;
    private readonly object _writeLock = new();
    private readonly ILogger<ConcurrentExchangeRateProvider> _logger;

    private ExchangeRateSnapshot _snapshot;
    private long _version;

    public ConcurrentExchangeRateProvider(
        IOptions<ExchangeRateOptions> options,
        ILogger<ConcurrentExchangeRateProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        var configured = options.Value.DkkPerHundred;

        ValidateConfiguration(configured);

        var now = DateTimeOffset.UtcNow;

        var initialRates = configured.ToDictionary(
            x => x.Key.ToUpperInvariant(),
            x => new ExchangeRate(
                x.Key.ToUpperInvariant(),
                x.Value,
                now),
            StringComparer.OrdinalIgnoreCase);

        _rates = new ConcurrentDictionary<string, ExchangeRate>(
            initialRates,
            StringComparer.OrdinalIgnoreCase);

        _version = 1;

        _snapshot = CreateSnapshot(_version, now);

        _logger.LogInformation(
            "Exchange-rate provider initialized. " +
            "CurrencyCount={Count}, Version={Version}",
            _rates.Count,
            _version);
    }

    public ValueTask<ExchangeRateSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            Volatile.Read(ref _snapshot));
    }

    public ValueTask UpdateRateAsync(
        string currency,
        decimal rate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UpdateRate(currency, rate, cancellationToken);

        return ValueTask.CompletedTask;
    }

    private void UpdateRate(
        string currency,
        decimal rate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        currency = currency.Trim().ToUpperInvariant();

        if (currency.Length != CurrencyConstants.IsoCodeLength ||
            !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Invalid ISO currency code.",
                nameof(currency));
        }

        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate),
                "Exchange rate must be positive.");
        }

        if (currency == CurrencyConstants.BaseCurrency)
        {
            throw new InvalidOperationException(
                "DKK reference rate cannot be modified.");
        }

        lock (_writeLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_rates.TryGetValue(currency, out var oldRate))
                throw new UnknownCurrencyException(currency);

            long nextVersion = checked(_version + 1);
            var now = DateTimeOffset.UtcNow;

            var nextRate = new ExchangeRate(
                currency, rate, now);

            // Build the replacement snapshot before publishing.
            var builder = ImmutableDictionary
                .CreateBuilder<string, ExchangeRate>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var code in _snapshot.Currencies)
            {
                builder[code] = code == currency
                    ? nextRate
                    : _rates[code];
            }

            var nextSnapshot = new ExchangeRateSnapshot(
                builder.ToImmutable(),
                nextVersion,
                now);

            cancellationToken.ThrowIfCancellationRequested();

            _rates[currency] = nextRate;
            _version = nextVersion;

            Volatile.Write(ref _snapshot, nextSnapshot);

            _logger.LogInformation(
                "Exchange rate updated. Currency={Currency}, " +
                "OldRate={OldRate}, NewRate={NewRate}, Version={Version}",
                currency,
                oldRate.DkkPerHundred,
                rate,
                nextVersion);
        }
    }

    private ExchangeRateSnapshot CreateSnapshot(
        long version,
        DateTimeOffset timestamp)
    {
        var copy = _rates.ToImmutableDictionary(
            x => x.Key,
            x => x.Value,
            StringComparer.OrdinalIgnoreCase);

        return new ExchangeRateSnapshot(
            copy,
            version,
            timestamp);
    }

    private static void ValidateConfiguration(
        Dictionary<string, decimal>? rates)
    {
        if (rates is null || rates.Count == 0)
        {
            throw new InvalidOperationException(
                "Exchange-rate configuration is empty.");
        }

        if (!rates.TryGetValue(
                CurrencyConstants.BaseCurrency,
                out decimal baseRate) ||
            baseRate != CurrencyConstants.BaseRatePerHundred)
        {
            throw new InvalidOperationException(
                "DKK reference rate must be 100.");
        }

        if (rates.Any(x =>
            x.Key.Length != CurrencyConstants.IsoCodeLength ||
            !x.Key.All(char.IsAsciiLetter) ||
            x.Value <= 0))
        {
            throw new InvalidOperationException(
                "Invalid currency code or exchange rate.");
        }

        if (rates.Keys.Distinct(
            StringComparer.OrdinalIgnoreCase).Count() != rates.Count)
        {
            throw new InvalidOperationException(
                "Duplicate currency codes detected.");
        }
    }
}

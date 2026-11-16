using FXExchange.Domain.Entities;
using FXExchange.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Application.Models
{
    public sealed class ExchangeRateSnapshot
    {
        private readonly ImmutableDictionary<string, ExchangeRate> _rates;

        public long Version { get; }
        public DateTimeOffset PublishedAtUtc { get; }

        public ExchangeRateSnapshot(
            ImmutableDictionary<string, ExchangeRate> rates,
            long version,
            DateTimeOffset publishedAtUtc)
        {
            _rates = rates ?? throw new ArgumentNullException(nameof(rates));
            Version = version;
            PublishedAtUtc = publishedAtUtc;
        }

        public decimal GetRate(string currency)
        {
            if (!_rates.TryGetValue(currency, out var rate))
                throw new UnknownCurrencyException(currency);

            return rate.DkkPerHundred;
        }

        public IReadOnlyCollection<string> Currencies =>
            _rates.Keys.ToArray();
    }
}

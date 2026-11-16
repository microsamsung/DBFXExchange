using FXExchange.Application.Abstractions;
using FXExchange.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Infrastructure.Factories
{
    public sealed class ConversionStrategyFactory
     : IConversionStrategyFactory
    {
        private readonly IReadOnlyList<IConversionStrategy> _strategies;

        public ConversionStrategyFactory(
            IEnumerable<IConversionStrategy> strategies)
        {
            ArgumentNullException.ThrowIfNull(strategies);

            _strategies = strategies.ToArray();

            if (_strategies.Count == 0)
                throw new InvalidOperationException(
                    "No conversion strategies are registered.");
        }

        public IConversionStrategy Create(CurrencyPair pair)
        {
            ArgumentNullException.ThrowIfNull(pair);

            var matches = _strategies
                .Where(x => x.CanHandle(pair))
                .Take(2)
                .ToArray();

            if (matches.Length != 1)
                throw new InvalidOperationException(
                    $"Expected exactly one conversion strategy for {pair}. " +
                    $"Found {matches.Length}.");

            return matches[0];
        }
    }
}

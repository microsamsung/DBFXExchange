using FXExchange.Application.Abstractions;
using FXExchange.Application.Models;
using FXExchange.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Infrastructure.Strategies
{
    public sealed class IdentityConversionStrategy : IConversionStrategy
    {
        public bool CanHandle(CurrencyPair pair)
        {
            ArgumentNullException.ThrowIfNull(pair);
            return pair.Source == pair.Target;
        }

        public decimal Convert(
            CurrencyPair pair,
            decimal amount,
            ExchangeRateSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(pair);
            ArgumentNullException.ThrowIfNull(snapshot);

            snapshot.GetRate(pair.Source);

            return amount;
        }
    }
}

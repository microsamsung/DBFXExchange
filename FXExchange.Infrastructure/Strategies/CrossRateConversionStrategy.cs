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
    public sealed class CrossRateConversionStrategy : IConversionStrategy
    {
        public bool CanHandle(CurrencyPair pair)
        {
            ArgumentNullException.ThrowIfNull(pair);
            return pair.Source != pair.Target;
        }

        public decimal Convert(
            CurrencyPair pair,
            decimal amount,
            ExchangeRateSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(pair);
            ArgumentNullException.ThrowIfNull(snapshot);

            decimal sourceRate = snapshot.GetRate(pair.Source);
            decimal targetRate = snapshot.GetRate(pair.Target);

            return checked(amount * sourceRate / targetRate);
        }
    }
}

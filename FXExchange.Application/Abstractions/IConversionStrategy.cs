using FXExchange.Application.Models;
using FXExchange.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Application.Abstractions
{
    public interface IConversionStrategy
    {
        bool CanHandle(CurrencyPair pair);

        decimal Convert(
            CurrencyPair pair,
            decimal amount,
            ExchangeRateSnapshot snapshot);
    }
}

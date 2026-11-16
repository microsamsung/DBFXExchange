using FXExchange.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Application.Abstractions
{
    public interface IConversionStrategyFactory
    {
        IConversionStrategy Create(CurrencyPair pair);
    }
}

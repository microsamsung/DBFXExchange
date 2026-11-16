using FXExchange.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Application.Abstractions
{
    public interface ICurrencyConverter
    {
        Task<decimal> ConvertAsync(
            CurrencyConversionRequest request,
            CancellationToken cancellationToken = default);
    }
}

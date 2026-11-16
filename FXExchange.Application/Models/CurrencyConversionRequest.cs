using FXExchange.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Application.Models
{
    public sealed record CurrencyConversionRequest(
        CurrencyPair Pair,
        decimal Amount);
}

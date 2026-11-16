using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Domain.Entities
{
    public sealed record ExchangeRate(
        string Currency,
        decimal DkkPerHundred,
        DateTimeOffset UpdatedAtUtc);

}

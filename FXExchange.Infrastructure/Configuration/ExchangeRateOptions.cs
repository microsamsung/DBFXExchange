using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Infrastructure.Configuration
{
    public sealed class ExchangeRateOptions
    {
        public const string SectionName = "ExchangeRates";

        public Dictionary<string, decimal> DkkPerHundred { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);
    }
}

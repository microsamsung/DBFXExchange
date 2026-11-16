using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Domain.Constants
{
    public static class CurrencyConstants
    {
        public const string BaseCurrency = "DKK";
        public const decimal BaseRatePerHundred = 100m;
        public const int IsoCodeLength = 3;
        public const char PairSeparator = '/';
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Domain.Exceptions
{
    public sealed class UnknownCurrencyException : Exception
    {
        public UnknownCurrencyException(string currency)
            : base($"Unknown currency: {currency}.")
        {
        }
    }
}

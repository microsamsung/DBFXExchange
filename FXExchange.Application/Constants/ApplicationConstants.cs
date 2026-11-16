using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Application.Constants
{
    public static class ApplicationConstants
    {
        public const string NegativeAmount =
            "Amount cannot be negative.";

        public const string InvalidRate =
            "Exchange rate must be greater than zero.";

        public const string ZeroAmount =
            "Exchange rate must not be zero. Any zero currency converts to ZERO :) ";
    }
}

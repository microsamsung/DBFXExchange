using FXExchange.Infrastructure.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FXExchange.Infrastructure.Configuration
{

    public sealed class RateSimulationOptions
    {
        public const string SectionName = "RateSimulation";

        public bool Enabled { get; set; }

        public int IntervalSeconds { get; set; }
            = InfrastructureConstants.DefaultIntervalSeconds;

        public decimal MaxChangePercent { get; set; }
            = InfrastructureConstants.DefaultMaxChangePercent;
    }
}

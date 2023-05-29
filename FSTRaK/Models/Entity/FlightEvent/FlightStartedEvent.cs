using System;

namespace FSTRaK.Models
{
    internal class FlightStartedEvent : BaseFlightEvent
    {
        public double FuelWeightLbs { get; set; }

    }
}

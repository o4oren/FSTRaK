using System;

namespace FSTRaK.Models
{
    internal class FlightStartedEvent : FlightEvent
    {
        public double FuelWeightLbs { get; set; }

    }
}

using System;

namespace FSTRaK.Models
{
    internal class FlightEndedEvent : FlightEvent
    {
        public double FuelWeightLbs { get; set; }

    }
}

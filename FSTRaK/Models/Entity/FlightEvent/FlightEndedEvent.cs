using System;

namespace FSTRaK.Models
{
    internal class FlightEndedEvent : BaseFlightEvent
    {
        public double FuelWeightLbs { get; set; }

    }
}

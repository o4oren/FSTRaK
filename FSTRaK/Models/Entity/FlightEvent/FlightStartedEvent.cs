using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    internal class FlightStartedEvent : BaseFlightEvent
    {
        [Column("FuelWeightLbs")]
        public double FuelWeightLbs { get; set; }

    }
}

using FSTRaK.DataTypes;
using System.ComponentModel.DataAnnotations.Schema;


namespace FSTRaK.Models
{
    internal class LandingEvent : ScoringEvent
    {
        [Column("FlapsPosition")]
        public double FlapsPosition { get; set; }
        public double VerticalSpeed { get; set; }

        [Column("FuelWeightLbs")]
        public double FuelWeightLbs { get; set; }

        public LandingRate LandingRate { get; set; }

        public double TouchDownPitchDegrees { get; set; }
        public double TouchDownBankDegress { get; set; }

        [NotMapped] public override string EventName { get; set; } = "Landing";

        public override string ToString()
        {
            return $"{LandingRate}\n" + base.ToString() + $"\n{VerticalSpeed:F0} ft/m";
        }

    }
}

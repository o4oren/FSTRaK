using FSTRaK.DataTypes;
using System.ComponentModel.DataAnnotations.Schema;


namespace FSTRaK.Models
{
    internal class LandingEvent : ScoringEvent
    {
        [Column("FlapsPosition")]
        public double FlapsPosition { get; set; }
        public double VerticalSpeed { get; set; }


        public LandingRate LandingRate { get; set; }

        public double TouchDownPitchDegrees { get; set; }
        public double TouchDownBankDegress { get; set; }

        public double? TouchdownGForce { get; set; }

        [NotMapped] public override string EventName { get; set; } = "Landing";

        public override string ToString()
        {
            var text = $"{LandingRate}\n" + base.ToString() + $"\n{VerticalSpeed:F0} fpm";
            if (TouchdownGForce != null)
            {
                text += $"\n{TouchdownGForce:F2} G";
            }
            return text;
        }

    }
}

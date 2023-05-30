using FSTRaK.DataTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public override int ScoreDelta { get; set; }
    }
}

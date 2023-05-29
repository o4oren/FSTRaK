using FSTRaK.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class LandingEvent : ScoringEvent
    {
        public double FlapsPosition { get; set; }
        public double VerticalSpeed { get; set; }
        public double FuelWeightLbs { get; set; }

        public LandingRate LandingRate { get; set; }

        public double TouchDownPitchDegrees { get; set; }
        public double TouchDownBankDegress { get; set; }
        public override int ScoreDelta { get; set; }
    }
}

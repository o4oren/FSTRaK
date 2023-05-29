using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models.Entity.FlightEvent
{
    internal class FlapsSpeedExceededEvent : ScoringEvent
    {
        public override int ScoreDelta { get; set; } = -5;
    }
}

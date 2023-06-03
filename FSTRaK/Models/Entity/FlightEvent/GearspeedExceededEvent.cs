using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models.Entity.FlightEvent
{
    internal class GearsSpeedExceededEvent : ScoringEvent
    {
        [NotMapped] public override string EventName { get; set; } = "Gear speed exceeded";

        public GearsSpeedExceededEvent() {
            ScoreDelta = -10;
        }

    }
}

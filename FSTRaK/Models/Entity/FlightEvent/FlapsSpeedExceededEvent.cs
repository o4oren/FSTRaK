using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models.Entity.FlightEvent
{
    internal class FlapsSpeedExceededEvent : ScoringEvent
    {
        [NotMapped] public override string EventName { get; set; } = "Flaps speed exceeded";

        public FlapsSpeedExceededEvent()
        {
            ScoreDelta = -5;
        }
    }
}

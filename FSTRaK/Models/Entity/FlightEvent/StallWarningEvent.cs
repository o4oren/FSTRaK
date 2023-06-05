using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models.Entity.FlightEvent
{
    internal class StallWarningEvent : ScoringEvent
    {

        [NotMapped] public override string EventName { get; set; } = "Stalled"; 
        public StallWarningEvent()
        {
            ScoreDelta = -20;
        }

    }
}

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    /// <summary>
    /// To be inherited by all Scoring events
    /// </summary>
    internal abstract class ScoringEvent : BaseFlightEvent
    {
        public int ScoreDelta { get; set; } = 0;
        [NotMapped] public abstract string EventName { get; set; }
    }
}

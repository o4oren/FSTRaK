using System;

namespace FSTRaK.Models
{
    /// <summary>
    /// To be inherited by all Scoring events
    /// </summary>
    internal abstract class ScoringEvent : FlightEvent
    {
        public abstract int ScoreDelta { get; set; }
    }
}

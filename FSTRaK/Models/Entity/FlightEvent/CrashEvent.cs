
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    internal class CrashEvent : ScoringEvent
    {

        [NotMapped] public override string EventName { get; set; } = "Crashed";

        public CrashEvent() 
        {
            ScoreDelta = -100;
        }
    }
}

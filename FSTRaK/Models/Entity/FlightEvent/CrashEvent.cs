
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    internal class CrashEvent : ScoringEvent
    {
        public override int ScoreDelta { get; set; } = -100;
    }
}

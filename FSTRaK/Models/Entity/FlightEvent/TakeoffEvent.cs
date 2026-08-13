using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class TakeoffEvent : BaseFlightEvent
    {
        [Column("FlapsPosition")]
        public double FlapsPosition { get; set; }

        [NotMapped] public override string EventName { get; set; } = "Takeoff";

    }
}

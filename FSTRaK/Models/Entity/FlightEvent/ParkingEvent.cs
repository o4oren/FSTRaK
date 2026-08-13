
using System.ComponentModel.DataAnnotations.Schema;

namespace FSTRaK.Models
{
    internal class ParkingEvent : BaseFlightEvent
    {
        [Column("FlapsPosition")]
        public double FlapsPosition { get; set; }
    }
}

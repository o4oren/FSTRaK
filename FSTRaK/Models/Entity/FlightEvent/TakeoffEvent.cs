using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class TakeoffEvent : FlightEvent
    {
        public double FlapsPosition { get; set; }
        public double FuelQuantityLbs { get; set; }

    }
}

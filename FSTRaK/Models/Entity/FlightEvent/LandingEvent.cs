using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class LandingEvent : FlightEvent
    {
        public double VerticalSpeed { get; set; }
    }
}

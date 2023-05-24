using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class TaxiOutEvent : FlightEvent
    {
        public double FuelWeightLbs { get; set; }
    }
}

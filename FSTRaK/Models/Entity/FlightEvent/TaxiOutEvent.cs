using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class TaxiOutEvent : BaseFlightEvent
    {
        public double FuelWeightLbs { get; set; }
    }
}

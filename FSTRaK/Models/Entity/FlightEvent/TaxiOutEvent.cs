using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class TaxiOutEvent : BaseFlightEvent
    {
        [Column("FuelWeightLbs")]
        public double FuelWeightLbs { get; set; }
    }
}

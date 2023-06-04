using FSTRaK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.ViewModels
{
    internal class FlightsListItemViewModel
    {
        private Flight _flight;
        public Flight Flight { get { return _flight; } }
        public FlightsListItemViewModel(Flight flight) {
            _flight = flight;
        }
    }
}

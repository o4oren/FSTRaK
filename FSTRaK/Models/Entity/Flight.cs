using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Policy;

namespace FSTRaK.Models
{
    internal class Flight : BaseModel
    {
        public int ID { get; set; }
        public virtual Aircraft Aircraft { get; set; }

        [Index(nameof(DepartureAirport))]
        public String DepartureAirport { get; set; }
        
        [Index(nameof(ArrivalAirport))]
        public String ArrivalAirport { get; set; }
        public ObservableCollection<FlightEvent> FlightEvents { get; private set; }

        public Flight()
        {
            this.FlightEvents = new ObservableCollection<FlightEvent>();
        }

    }
}

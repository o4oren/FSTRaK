using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FSTRaK.Models
{
    internal class Flight : BaseModel
    {
        public int ID { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime OffTime { get; set; }
        public DateTime OnTime { get; set; }
        public virtual Aircraft Aircraft { get; set; }
        public String DepartureAirport { get; set; }
        public String ArrivalAirport { get; set; }

        public virtual ObservableCollection<FlightEvent> FlightEvents { get; set; } = new ObservableCollection<FlightEvent>();

    }
}

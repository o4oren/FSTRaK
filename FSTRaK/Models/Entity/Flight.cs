using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FSTRaK.Models
{
    internal class Flight : BaseModel
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime OffTime { get; set; }
        public DateTime OnTime { get; set; }
        public Aircraft Aircraft { get; set; }
        public String DepartureAirport { get; set; }
        public String ArrivalAirport { get; set; }

        public ObservableCollection<FlightEvent> FlightEvents = new ObservableCollection<FlightEvent>();

    }
}

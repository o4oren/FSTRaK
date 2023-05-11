using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FSTRaK.Models
{
    internal class Flight : BaseModel
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime OffTime { get; set; }
        public DateTime OnTime { get; set; }
        public Aircraft Aircraft { get; set; }

        public String DepartureAirport { get; set; }
        public String ArrivalAirport { get; set; }

        public ObservableCollection<FlightEvent> FlightEvents = new ObservableCollection<FlightEvent>();

        // Calculated properties
        public double Heading
        {
            get
            {
                if (FlightEvents.Count > 0)
                    return FlightEvents.Last().TrueHeading;
                return 0;
            }
        }

        public double Latitude
        {
            get
            {
                if (FlightEvents.Count > 0)
                    return FlightEvents.Last().Latitude;
                return 0;
            }
        }

        public double Longitude
        {
            get
            {
                if (FlightEvents.Count > 0)
                    return FlightEvents.Last().Longitude;
                return 0;
            }
        }

    }
}

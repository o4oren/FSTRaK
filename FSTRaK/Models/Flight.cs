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

        // Current location
        private double _heading = 0;
        public double Heading
        {
            get
            {
                return _heading;
            }
            set 
            { 
                if(value != _heading)
                {
                    _heading = value;
                }
            }
        }

        private double _latitude = 0;
        public double Latitude
        {
            get
            {
                return _latitude;
            }
            set
            {
                if (value != _latitude)
                {
                    _latitude = value;
                }
            }
        }


        private double _longitude = 0;
        public double Longitude
        {
            get
            {
                return _longitude;
            }
            set
            {
                if (value != _longitude)
                {
                    _longitude = value;
                }
            }
        }

    }
}

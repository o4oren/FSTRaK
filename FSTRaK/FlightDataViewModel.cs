using MapControl;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Device.Location;
using System.Drawing.Text;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK
{
    internal class FlightDataViewModel : INotifyPropertyChanged
    {
        SimConnectManager smc = SimConnectManager.Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title { get; set; }
        public double[] Position { get; set; } = new double[0];
        public LocationCollection FlightPath { get; set; } = new LocationCollection();
        public string Details { get; set; }
        public double Heading { get; set; }

        private DateTime _lastUpdated = DateTime.Now;
        public string Path { get; set; } = "";

        public SimConnectManager.AircraftFlightData FlightData
        {
            get
            {
                return smc.FlightData;
            }
        }

        public FlightDataViewModel() { 
            smc.PropertyChanged += updateFlightDataView;
        }


        private void updateFlightDataView(object sender, PropertyChangedEventArgs e)
        {
            SimConnectManager.AircraftFlightData a = smc.FlightData;
            Title = smc.FlightData.title;
            Details = $"Lat: {a.latitude:F4} Lon:{a.longitude:F4} \nHeading: {a.trueHeading:F0} Alt: {a.altitude:F0} ft\nSpeed: {a.airspeed:F0} Knots";
            Position = new double[] { a.latitude, a.longitude, a.trueHeading };
            Heading = a.trueHeading;
            

            OnPropertyChanged("Title");
            OnPropertyChanged("Details");
            OnPropertyChanged("Position");

            OnPropertyChanged("Heading");
            OnPropertyChanged("FlightData");

            if (_lastUpdated.AddSeconds(2) < DateTime.Now) {
                FlightPath.Add(a.latitude, a.longitude);
                _lastUpdated = DateTime.Now;
                OnPropertyChanged("FlightPath");
            }



            Log.Information(Title);
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

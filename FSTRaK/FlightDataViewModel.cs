using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK
{
    internal class FlightDataViewModel : INotifyPropertyChanged
    {
        SimConnectManager smc = SimConnectManager.Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title { get; set; }
        public double[] Position { get; set; }
        public string Details { get; set; }
        public double Heading { get; set; }

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

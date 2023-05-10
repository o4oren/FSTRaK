using FSTRaK.Models;
using MapControl;
using System;
using System.ComponentModel;


namespace FSTRaK.ViewModels
{
    internal class FlightDataViewModel : BaseViewModel
    {
        SimConnectService smc = SimConnectService.Instance;

        public string Title {
            get
            {
                if(smc.Aircraft != null)
                    return smc.Aircraft.Title;
                return "";
            }
        }

        public string Model
        {
            get
            {
                if (smc.Aircraft != null)
                    return smc.Aircraft.Model;
                return "";
            }
        }

        public string Manufacturer
        {
            get
            {
                if (smc.Aircraft != null)
                    return smc.Aircraft.Manufacturer;
                return "";
            }
        }

        public string Airline
        {
            get
            {
                if (smc.Aircraft != null)
                    return smc.Aircraft.Airline;
                return "";
            }
        }

        public double[] Position { get; set; } = new double[0];
        public LocationCollection FlightPath { get; set; } = new LocationCollection();
        public string Details { get; set; }
        public double Heading { get; set; }

        private DateTime _lastUpdated = DateTime.Now;

        public SimConnectService.AircraftFlightData FlightData
        {
            get
            {
                return smc.FlightData;
            }
        }

        public FlightDataViewModel()
        {
            smc.PropertyChanged += SimconnectManagerUpdate;
        }


        private void SimconnectManagerUpdate(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case ("Aircraft"):
                    OnPropertyChanged("Title");
                    OnPropertyChanged("Model");
                    OnPropertyChanged("Manufacturer");
                    OnPropertyChanged("Airline");

                    break;
                case ("FlightData"):
                    SimConnectService.AircraftFlightData a = smc.FlightData;
                    Details = $"Lat: {a.latitude:F4} Lon:{a.longitude:F4} \nHeading: {a.trueHeading:F0} Alt: {a.altitude:F0} ft\nSpeed: {a.airspeed:F0} Knots";
                    Position = new double[] { a.latitude, a.longitude, a.trueHeading };
                    Heading = a.trueHeading;


                    OnPropertyChanged("Details");
                    OnPropertyChanged("Position");

                    OnPropertyChanged("Heading");
                    OnPropertyChanged("FlightData");

                    if (_lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(a.latitude, a.longitude);
                        _lastUpdated = DateTime.Now;
                        OnPropertyChanged("FlightPath");
                    }
                    break;
                default:
                    break;
            }



        }
    }
}
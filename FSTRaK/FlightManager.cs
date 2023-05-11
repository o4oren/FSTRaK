using FSTRaK.Models;
using FSTRaK.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK
{
    internal sealed class FlightManager : INotifyPropertyChanged
    {
        private static readonly object _lock = new object();
        private static FlightManager instance = null;
        private FlightManager() { }

        public event PropertyChangedEventHandler PropertyChanged;

        private SimConnectService _simConnectService;
        public static FlightManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new FlightManager();
                    }
                    return instance;
                }
            }
        }

        internal void Initialize()
        {
            _simConnectService = SimConnectService.Instance;
            _simConnectService.Initialize();
            _simConnectService.PropertyChanged += SimconnectService_OnPropertyChange;
        }



        // Properties
        private Aircraft _aircraft;
        public Aircraft Aircraft
        {
            get { return _aircraft; }
            set
            {
                if (value != _aircraft)
                {
                    _aircraft = value;
                    OnPropertyChanged();
                }
            }
        }

        string _nearestAirport = string.Empty;
        public string NearestAirport
        {
            get { return _nearestAirport; }
            set
            {
                if(value != _nearestAirport)
                {
                    _nearestAirport = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SimconnectService_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            
            switch(e.PropertyName)
            {
                case "FlightData":
                    var data = _simConnectService.FlightData;
                    Aircraft aircraft = new Aircraft();
                    aircraft.Title = data.title;
                    aircraft.Manufacturer = data.atcType;
                    aircraft.Model = data.model;
                    aircraft.Airline = data.airline;
                    aircraft.Heading = data.trueHeading;
                    aircraft.Position = new double[] { data.latitude, data.longitude };
                    aircraft.Altitude = data.altitude;
                    aircraft.Airspeed = data.airspeed;
                    Aircraft = aircraft;
                    break;
                case "NearestAirport":
                    var airport = _simConnectService.NearestAirport;
                    NearestAirport =airport;
                    break;
            }

        }



        public void Close()
        {
            _simConnectService?.Close();
        }


        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

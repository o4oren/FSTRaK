using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using Serilog;

namespace FSTRaK.ViewModels
{
    internal class EditFlightViewModel : BaseViewModel
    {
        private const int NearbyAirportsCount = 5;

        private readonly Flight _flight;

        public class AirportOption
        {
            public string Ident { get; set; }
            public string Label { get; set; }
        }

        private string _departureAirport;
        public string DepartureAirport
        {
            get => _departureAirport;
            set
            {
                _departureAirport = value;
                OnPropertyChanged();
            }
        }

        private string _arrivalAirport;
        public string ArrivalAirport
        {
            get => _arrivalAirport;
            set
            {
                _arrivalAirport = value;
                OnPropertyChanged();
            }
        }

        private List<AirportOption> _nearbyDepartureAirports = new List<AirportOption>();
        public List<AirportOption> NearbyDepartureAirports
        {
            get => _nearbyDepartureAirports;
            set
            {
                _nearbyDepartureAirports = value;
                OnPropertyChanged();
            }
        }

        private List<AirportOption> _nearbyArrivalAirports = new List<AirportOption>();
        public List<AirportOption> NearbyArrivalAirports
        {
            get => _nearbyArrivalAirports;
            set
            {
                _nearbyArrivalAirports = value;
                OnPropertyChanged();
            }
        }

        private string _validationMessage;
        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                _validationMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _wasUpdated = false;
        public bool WasUpdated
        {
            get => _wasUpdated;
            set
            {
                _wasUpdated = value;
                OnPropertyChanged();
            }
        }

        private bool _isShow = true;
        public bool IsShow
        {
            get => _isShow;
            set
            {
                _isShow = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand UpdateFlight { get; }
        public RelayCommand ClosePopup { get; }

        public EditFlightViewModel(Flight flight) : base()
        {
            _flight = flight;
            DepartureAirport = flight.DepartureAirport;
            ArrivalAirport = flight.ArrivalAirport;

            Task.Run(() => LoadNearbyAirports(flight));

            UpdateFlight = new RelayCommand(o =>
            {
                var departure = NormalizeIdent(DepartureAirport);
                var arrival = NormalizeIdent(ArrivalAirport);

                var airports = AirportResolver.Instance.AirportsDictionary;
                var unknown = new List<string>();
                if (!airports.ContainsKey(departure))
                    unknown.Add(departure);
                if (!airports.ContainsKey(arrival))
                    unknown.Add(arrival);
                if (unknown.Any())
                {
                    ValidationMessage = $"Unknown airport code: {string.Join(", ", unknown)}";
                    return;
                }
                ValidationMessage = string.Empty;

                // Mutate the bound entity on the UI thread; only the save runs in the background.
                _flight.DepartureAirport = departure;
                _flight.ArrivalAirport = arrival;

                Task.Run(() =>
                {
                    using (var logbookContext = new LogbookContext())
                    {
                        try
                        {
                            logbookContext.Entry(_flight).State = EntityState.Modified;
                            logbookContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, ex.Message);
                        }
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            WasUpdated = true;
                            IsShow = false;
                        });
                    }
                });
            });

            ClosePopup = new RelayCommand(o =>
            {
                IsShow = false;
            });
        }

        private static string NormalizeIdent(string ident)
        {
            return (ident ?? string.Empty).Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Offers the airports closest to where the flight actually started and ended,
        /// for when the sim's nearest-airport detection picked a neighboring field.
        /// </summary>
        private void LoadNearbyAirports(Flight flight)
        {
            try
            {
                var firstEvent = flight.FlightEvents?.FirstOrDefault();
                var lastEvent = flight.FlightEvents?.LastOrDefault();

                var nearbyDeparture = firstEvent != null
                    ? FindNearestAirports(firstEvent.Latitude, firstEvent.Longitude)
                    : FindNearestAirportsToIdent(flight.DepartureAirport);
                var nearbyArrival = lastEvent != null
                    ? FindNearestAirports(lastEvent.Latitude, lastEvent.Longitude)
                    : FindNearestAirportsToIdent(flight.ArrivalAirport);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    NearbyDepartureAirports = nearbyDeparture;
                    NearbyArrivalAirports = nearbyArrival;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load nearby airports for flight editing");
            }
        }

        private static List<AirportOption> FindNearestAirportsToIdent(string ident)
        {
            if (ident != null && AirportResolver.Instance.AirportsDictionary.TryGetValue(ident, out var airport))
            {
                return FindNearestAirports(airport.latitude_deg, airport.longitude_deg);
            }
            return new List<AirportOption>();
        }

        private static List<AirportOption> FindNearestAirports(double latitude, double longitude)
        {
            return AirportResolver.Instance.AirportsDictionary.Values
                .Select(a => new
                {
                    Airport = a,
                    Distance = GeodesicUtil.DistanceNm(latitude, longitude, a.latitude_deg, a.longitude_deg)
                })
                .OrderBy(x => x.Distance)
                .Take(NearbyAirportsCount)
                .Select(x => new AirportOption
                {
                    Ident = x.Airport.ident,
                    Label = $"{x.Airport.ident} - {x.Airport.name} ({x.Distance:F1} nm)"
                })
                .ToList();
        }
    }
}

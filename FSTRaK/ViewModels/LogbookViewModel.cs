using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Models.FlightManager;
using MapControl;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using FSTRaK.Models.FlightManager.State;

namespace FSTRaK.ViewModels
{
    internal class LogbookViewModel : BaseViewModel
    {
        FlightManager _flightManager = FlightManager.Instance;

        private System.Timers.Timer _typingTimer;
        public RelayCommand OnLoogbookLoadedCommand { get; set; }
        public RelayCommand DeleteFlightCommand { get; set; }

        private FlightDetailsViewModel _flightDetailsViewModel;

        public FlightDetailsViewModel FlightDetailsViewModel { 
            get { return _flightDetailsViewModel; }
            private set 
            { 
                _flightDetailsViewModel = value;
                OnPropertyChanged();
            } 
        }

        public ObservableCollection<Flight> Flights { get; set; }

        private Flight _selectedFlight;
        public Flight SelectedFlight { get 
            {
                if (_selectedFlight == null)
                {
                    return new Flight();
                }
                return _selectedFlight;
            } 
            set 
            {
                if(value != null && _selectedFlight != value)
                {
                    _selectedFlight = value;
                    _flightDetailsViewModel.Flight = _selectedFlight;
                    OnPropertyChanged(nameof(SelectedFlight));
                }
            } 
        }

        public LogbookViewModel() 
        {
            Flights = new ObservableCollection<Flight>();
            _flightDetailsViewModel = new FlightDetailsViewModel();
            _typingTimer = new System.Timers.Timer(500);

            _flightManager.PropertyChanged += async (s,e) =>
            {
                if(e.PropertyName.Equals(nameof(_flightManager.State)) && (_flightManager.State is FlightEndedState))
                {
                    using (var logbookContext = new LogbookContext())
                    {
                        try
                        {
                            await LoadFlights(500);
                            var latestId = logbookContext.Flights.Max(f => f.Id);
                            SelectedFlight = logbookContext.Flights
                            .Where(f => f.Id == latestId)
                            .Include(f => f.Aircraft)
                            .Include(f => f.FlightEvents)
                            .SingleOrDefault();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, ex.Message);
                        }

                    }
                }
            };

            OnLoogbookLoadedCommand = new RelayCommand(o =>
            {
                LoadFlights();
            });

            DeleteFlightCommand = new RelayCommand(o =>
            {
                Task.Run(() =>
                {
                    using (var logbookContext = new LogbookContext())
                    {
                        try
                        {
                            logbookContext.Flights.Attach(SelectedFlight);
                            logbookContext.Flights.Remove(SelectedFlight);
                            logbookContext.SaveChanges();
                            LoadFlights();
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, ex.Message);
                        }
                    }
                });

            });

            _typingTimer.Elapsed += _typingTimer_Elapsed;
        }

        private void _typingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            SearchFlights();
            _typingTimer.Stop();
        }

        private string _searchText;
        public string SearchText 
        { 
            get { return _searchText; }
            set
            {
                _typingTimer.Stop();
                _typingTimer.Start();
                _searchText = value;
                // Actual search is in the typingTimerElapsed event handler.
            }
        }
    

        private Task LoadFlights()
        {
            return LoadFlights(0);
        }
        private Task LoadFlights(int delay)
        {
            return Task.Run(() => {

                Thread.Sleep(delay);
                using (var logbookContext = new LogbookContext())
                {
                    try
                    {
                        var flights = logbookContext.Flights
                        .Select(f => f)
                        .Include(f => f.Aircraft)
                        .Include(f => f.FlightEvents);

                        App.Current.Dispatcher.Invoke((Action)delegate
                        {
                            Flights = new ObservableCollection<Flight>(flights);
                            OnPropertyChanged(nameof(Flights));
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unhandled error occured!");
                    }
                }
            });
        }

        private Task SearchFlights()
        {
            if(SearchText == null || SearchText.Equals(string.Empty))
                return LoadFlights();

            return Task.Run(() => {
                using (var logbookContext = new LogbookContext())
                {
                    try
                    {
                        var flights = logbookContext.Flights
                        .Where(f => 
                            f.DepartureAirport.ToLower().Equals(SearchText.ToLower())
                            || f.ArrivalAirport.ToLower().Equals(SearchText.ToLower())
                            || f.Aircraft.Title.ToLower().Contains(SearchText.ToLower())
                            || f.Aircraft.Model.ToLower().Contains(SearchText.ToLower())
                            )
                        .Include(f => f.Aircraft)
                        .Include(f => f.FlightEvents);

                        App.Current.Dispatcher.Invoke((Action)delegate
                        {
                            Flights = new ObservableCollection<Flight>(flights);
                            OnPropertyChanged(nameof(Flights));
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception fetching Flights!");
                    }
                }
            });
        }
    }
}

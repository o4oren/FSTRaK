﻿using FSTRaK.Models;
using FSTRaK.Models.Entity;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using FSTRaK.BusinessLogic.FlightManager;
using FSTRaK.BusinessLogic.FlightManager.State;
using Newtonsoft.Json.Linq;

namespace FSTRaK.ViewModels
{
    internal class LogbookViewModel : BaseViewModel
    {
        FlightManager _flightManager = FlightManager.Instance;

        private System.Timers.Timer _typingTimer;
        public RelayCommand OnLoogbookLoadedCommand { get; set; }
        public RelayCommand DeleteFlightCommand { get; set; }
        public RelayCommand OpenAddCommentPopupCommand { get; set; }
        public RelayCommand OpenEditAircraftPopupCommand { get; set; }
        public RelayCommand CloseEditAircraftPopupCommand { get; set; }
        
        private FlightDetailsViewModel _flightDetailsViewModel;

        public FlightDetailsViewModel FlightDetailsViewModel { 
            get => _flightDetailsViewModel;
            private set 
            { 
                _flightDetailsViewModel = value;
                OnPropertyChanged();
            } 
        }

        private bool _showAddCommentPopup = false;

        public bool ShowAddCommentPopup
        {
            get => _showAddCommentPopup;
            set
            {
                _showAddCommentPopup = value;
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
                if (value == null || _selectedFlight == value) return;
                try
                {
                    if (value.FlightEvents.Count == 0)
                    {
                        using (var logbookContext = new LogbookContext())
                        {
                            ObservableCollection<BaseFlightEvent> flightEvents = new ObservableCollection<BaseFlightEvent>(logbookContext.FlightEvents.Where(fe => fe.FlightId == value.Id).ToList());
                            value.FlightEvents = flightEvents;
                        }
                    }
                    _selectedFlight = value;
                    _flightDetailsViewModel.Flight = _selectedFlight;
                    OnPropertyChanged();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception fetching Flights!");
                }
            } 
        }

        private EditAircraftViewModel _editAircraftViewModel;
        public EditAircraftViewModel EditAircraftViewModel
        {
            get => _editAircraftViewModel;
            set {
            if (value != null && _editAircraftViewModel != value) 
            {
                _editAircraftViewModel = value;
                OnPropertyChanged();
            }
            }
        }

        private AddCommentViewModel _addCommentViewModel;
        public AddCommentViewModel AddCommentViewModel
        {
            get => _addCommentViewModel;
            set
            {
                if (value != null && _addCommentViewModel != value)
                {
                    _addCommentViewModel = value;
                    OnPropertyChanged();
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
                            if (logbookContext.Flights == null)
                                return;
                            var latestId = logbookContext.Flights.Max(f => f.Id);
                            SelectedFlight = logbookContext.Flights
                            .Where(f => f.Id == latestId)
                            .Include(f => f.Aircraft)
                            .SingleOrDefault();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, ex.Message);
                        }

                    }
                }
            };


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

            OpenEditAircraftPopupCommand = new RelayCommand(o =>
            {
                
                var editAircraftViewModel = new EditAircraftViewModel(SelectedFlight.Aircraft)
                {
                    IsShow = true
                };
                editAircraftViewModel.PropertyChanged += (sender, args) =>
                {
                    if(editAircraftViewModel.WasUpdated)
                        LoadFlights();
                };
                EditAircraftViewModel = editAircraftViewModel;
            });

            OpenAddCommentPopupCommand = new RelayCommand(o => {
                var addCommentViewModel = new AddCommentViewModel(SelectedFlight)
                {
                    IsShow = true
                };
                addCommentViewModel.PropertyChanged += (sender, args) =>
                {
                    if (addCommentViewModel.WasUpdated)
                        LoadFlights();
                };
                AddCommentViewModel = addCommentViewModel;
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
            get => _searchText;
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
                            .OrderByDescending(f => f.Id)
                            .Include(f => f.Aircraft);

                        System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            Flights = new ObservableCollection<Flight>(flights);
                            OnPropertyChanged(nameof(Flights));
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unhandled error occurred!");
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
                            || f.Aircraft.Airline.ToLower().StartsWith(SearchText.ToLower())
                            || f.Aircraft.TailNumber.ToLower().StartsWith(SearchText.ToLower())
                        )
                        .OrderByDescending(f => f.Id)
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

        internal async void OnLoad()
        {
            Log.Information("on load");
            await LoadFlights(0);
            Flight flight = new Flight();
            using (var logbookContext = new LogbookContext())
            {
                if (SelectedFlight == null)
                {
                    var latestId = logbookContext.Flights.Max(f => f.Id);
                    flight = Flights
                        .SingleOrDefault(f => f.Id == latestId);
                    SelectedFlight = flight;
                }
                else
                {
                    SelectedFlight = _selectedFlight; // not workig
                }
            }
            
        }
    }
}

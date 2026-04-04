using FSTRaK.Models;
using FSTRaK.Models.Entity;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

using FSTRaK.BusinessLogic.FlightManager;

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

        public FlightDetailsViewModel FlightDetailsViewModel
        {
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
        public Flight SelectedFlight
        {
            get
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
                _selectedFlight = value;
                _flightDetailsViewModel.Flight = _selectedFlight;
                OnPropertyChanged();

                var flight = value;
                if ((flight.FlightEvents?.Count ?? 0) == 0)
                {
                    Log.Debug("SelectedFlight: starting async event load for flight {FlightId}", flight.Id);
                    Task.Run(() =>
                    {
                        try
                        {
                            using (var logbookContext = new LogbookContext())
                            {
                                var flightEvents = new ObservableCollection<BaseFlightEvent>(
                                    logbookContext.FlightEvents.Where(fe => fe.FlightId == flight.Id).ToList());
                                Log.Debug("SelectedFlight: loaded {Count} events for flight {FlightId}", flightEvents.Count, flight.Id);
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    flight.FlightEvents = flightEvents;
                                    if (_selectedFlight == flight)
                                    {
                                        Log.Debug("SelectedFlight: calling OnFlightEventsLoaded for flight {FlightId}", flight.Id);
                                        try
                                        {
                                            _flightDetailsViewModel.OnFlightEventsLoaded();
                                        }
                                        catch (Exception innerEx)
                                        {
                                            Log.Error(innerEx, "Exception in OnFlightEventsLoaded for flight {FlightId}!", flight.Id);
                                        }
                                    }
                                    else
                                    {
                                        Log.Debug("SelectedFlight: skipping OnFlightEventsLoaded — selected flight changed (expected {Expected}, actual {Actual})", flight.Id, _selectedFlight?.Id);
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception fetching flight events for flight {FlightId}!", flight.Id);
                        }
                    });
                }
                else
                {
                    Log.Debug("SelectedFlight: flight {FlightId} already has {Count} events loaded", flight.Id, flight.FlightEvents.Count);
                }
            }
        }

        private EditAircraftViewModel _editAircraftViewModel;
        public EditAircraftViewModel EditAircraftViewModel
        {
            get => _editAircraftViewModel;
            set
            {
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

        private Task _initialLoadTask;

        public LogbookViewModel()
        {
            Flights = new ObservableCollection<Flight>();
            _flightDetailsViewModel = new FlightDetailsViewModel();
            _typingTimer = new System.Timers.Timer(500);

            _flightManager.FlightSaved += (s, savedId) =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    LoadFlights(newFlightId: savedId, search: _searchText);
                });
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
                PropertyChangedEventHandler handler = null;
                handler = (sender, args) =>
                {
                    if (editAircraftViewModel.WasUpdated)
                    {
                        editAircraftViewModel.PropertyChanged -= handler;
                        LoadFlights(search: _searchText);
                    }
                };
                editAircraftViewModel.PropertyChanged += handler;
                EditAircraftViewModel = editAircraftViewModel;
            });

            OpenAddCommentPopupCommand = new RelayCommand(o =>
            {
                var addCommentViewModel = new AddCommentViewModel(SelectedFlight)
                {
                    IsShow = true
                };
                PropertyChangedEventHandler handler = null;
                handler = (sender, args) =>
                {
                    if (addCommentViewModel.WasUpdated)
                    {
                        addCommentViewModel.PropertyChanged -= handler;
                        LoadFlights(search: _searchText);
                    }
                };
                addCommentViewModel.PropertyChanged += handler;
                AddCommentViewModel = addCommentViewModel;
            });

            _typingTimer.Elapsed += _typingTimer_Elapsed;

            _initialLoadTask = App.DbWarmupTask.ContinueWith(_ => LoadFlights()).Unwrap();
        }

        private void _typingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            LoadFlights(search: _searchText);
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


        private Task LoadFlights(int? newFlightId = null, string search = null)
        {
            return Task.Run(() =>
            {
                using (var logbookContext = new LogbookContext())
                {
                    try
                    {
                        IQueryable<Flight> query = logbookContext.Flights
                            .OrderByDescending(f => f.Id)
                            .Include(f => f.Aircraft);

                        if (!string.IsNullOrEmpty(search))
                        {
                            var s = search.ToLower();
                            query = query.Where(f =>
                                f.DepartureAirport.ToLower().Equals(s)
                                || f.ArrivalAirport.ToLower().Equals(s)
                                || f.Aircraft.Title.ToLower().Contains(s)
                                || f.Aircraft.Model.ToLower().Contains(s)
                                || f.Aircraft.Airline.ToLower().StartsWith(s)
                                || f.Aircraft.TailNumber.ToLower().StartsWith(s)
                            );
                        }

                        var dbFlights = query.ToList();

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            // Remove flights no longer in the result
                            var dbIds = new HashSet<int>(dbFlights.Select(f => f.Id));
                            foreach (var existing in Flights.ToList())
                            {
                                if (!dbIds.Contains(existing.Id))
                                    Flights.Remove(existing);
                            }

                            // Update existing and add new
                            var existingById = Flights.ToDictionary(f => f.Id);
                            foreach (var dbFlight in dbFlights)
                            {
                                if (existingById.TryGetValue(dbFlight.Id, out var existing))
                                {
                                    // Update editable properties in place
                                    existing.Comment = dbFlight.Comment;
                                    existing.Aircraft = dbFlight.Aircraft;
                                }
                                else
                                {
                                    // New flight — insert at index 0 (list is descending by Id)
                                    Flights.Insert(0, dbFlight);
                                }
                            }

                            // Select the newly saved flight if requested
                            if (newFlightId.HasValue)
                            {
                                var saved = Flights.FirstOrDefault(f => f.Id == newFlightId.Value);
                                if (saved != null)
                                    SelectedFlight = saved;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Unhandled error occurred in LoadFlights!");
                    }
                }
            });
        }

        internal void OnLoad()
        {
            if (_selectedFlight != null) return;

            if (_initialLoadTask.IsCompleted)
            {
                SelectedFlight = Flights.FirstOrDefault();
            }
            else
            {
                _initialLoadTask.ContinueWith(_ =>
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (_selectedFlight == null)
                            SelectedFlight = Flights.FirstOrDefault();
                    }));
            }
        }
    }
}

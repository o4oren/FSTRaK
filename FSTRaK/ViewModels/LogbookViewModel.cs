using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Models.FlightManager;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;

namespace FSTRaK.ViewModels
{
    internal class LogbookViewModel : BaseViewModel
    {
        LogbookContext _logbookContext = new LogbookContext();
        FlightManager _flightManager = FlightManager.Instance;
        public RelayCommand OnLoogbookLoadedCommand { get; set; }

        public ObservableCollection<Flight> Flights { get; set; }

        public int SelectedFlightId { get; set; }


        public LogbookViewModel() 
        {
            Flights = new ObservableCollection<Flight>();

            _flightManager.PropertyChanged += ((s,e) =>
            {
                if(e.PropertyName.Equals(nameof(_flightManager.State)) && (_flightManager.State is FlightEndedState))
                {
                    LoadFlights(500);
                }
            });

            OnLoogbookLoadedCommand = new RelayCommand(o =>
            {
                LoadFlights();
            });


        }

        private Task LoadFlights()
        {
            return LoadFlights(0);
        }
        private Task LoadFlights(int delay)
        {
            return Task.Run(() => {
                Thread.Sleep(delay);
                var flights = _logbookContext.Flights.Select(f => f).Include(f => f.Aircraft);
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    Flights = new ObservableCollection<Flight>(flights);
                    OnPropertyChanged(nameof(Flights));
                });
            });
        }
    }
}

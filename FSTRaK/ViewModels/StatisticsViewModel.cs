using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using FSTRaK.Models;
using FSTRaK.Models.Entity;

namespace FSTRaK.ViewModels
{
    internal class StatisticsViewModel : BaseViewModel
    {
        private ObservableCollection<Flight> _flights;


        private int _totalNumberOfFlights;
        public int TotalNumberOfFlights
        {
            get => _totalNumberOfFlights;
            set
            {
                _totalNumberOfFlights = value;
                OnPropertyChanged();
            }
        }



        private string _totalFlightTime;
        public string TotalFlightTime
        {
            get => _totalFlightTime;
            set
            {
                _totalFlightTime = value;
                OnPropertyChanged();
            }
        }

        private string _avgFlightTime;
        public string AvgFlightTime
        {
            get => _avgFlightTime;
            set
            {
                _avgFlightTime = value;
                OnPropertyChanged();
            }
        }

        private string _totalFlightDistance;
        public string TotalFlightDistance
        {
            get => _totalFlightDistance;
            set
            {
                _totalFlightDistance = value;
                OnPropertyChanged();
            }
        }

        private string _avgFlightDistance;
        public string AvgFlightDistance
        {
            get => _avgFlightDistance;
            set
            {
                _avgFlightDistance = value;
                OnPropertyChanged();
            }
        }


        public StatisticsViewModel()
        {
            UpdateStatistics();

        }

        private void UpdateStatistics()
        {
            using var logbookContext = new LogbookContext();
            var flights = logbookContext.Flights
                .Select(f => f)
                .OrderByDescending(f => f.Id)
                .Include(f => f.Aircraft);
            
            TotalNumberOfFlights = flights.Count();
            
            var totalFlightMilis = flights.Sum(f => f.FlightTimeMilis);
            var averageFlightMilis = flights.Average(f => f.FlightTimeMilis);
            var totalFlightTimeTs = TimeSpan.FromTicks(totalFlightMilis);
            var avgFlightTimeTs = TimeSpan.FromTicks((long)averageFlightMilis);
            TotalFlightTime = $"{(int)totalFlightTimeTs.TotalHours}:{totalFlightTimeTs.Minutes}:{totalFlightTimeTs.Seconds}";
            AvgFlightTime = $"{(int)avgFlightTimeTs.TotalHours}:{avgFlightTimeTs.Minutes}:{avgFlightTimeTs.Seconds}";

            TotalFlightDistance = $"{flights.Sum(f => f.FlightDistanceNm).ToString("N1")}";
            AvgFlightDistance = $"{flights.Average(f => f.FlightDistanceNm).ToString("N1")}";

        }
    }
}

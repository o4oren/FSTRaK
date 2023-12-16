using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Media3D;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;

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

        private string _totalFuelUsed;
        public string TotalFuelUsed
        {
            get => _totalFuelUsed;
            set
            {
                _totalFuelUsed = value;
                OnPropertyChanged();
            }
        }

        private string _avgFuelUsed;
        public string AvgFuelUsed
        {
            get => _avgFuelUsed;
            set
            {
                _avgFuelUsed = value;
                OnPropertyChanged();
            }
        }

        private string _totalPayload;
        public string TotalPayload
        {
            get => _totalPayload;
            set
            {
                _totalPayload = value;
                OnPropertyChanged();
            }
        }

        private string _avgPayload;
        public string AvgPayload
        {
            get => _avgPayload;
            set
            {
                _avgPayload = value;
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

            TotalFlightDistance = $"{flights.Sum(f => f.FlightDistanceNm):N1}";
            AvgFlightDistance = $"{flights.Average(f => f.FlightDistanceNm):N1}";

            TotalFuelUsed = UnitsUtil.GetWeightString(flights.Sum(f => f.TotalFuelUsed));
            AvgFuelUsed = UnitsUtil.GetWeightString(flights.Average(f => f.TotalFuelUsed));


            TotalPayload = UnitsUtil.GetWeightString(flights.Sum(f => f.TotalPayloadLbs));
            AvgPayload = UnitsUtil.GetWeightString(flights.Average(f => f.TotalPayloadLbs));

        }
    }
}

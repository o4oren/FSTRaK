using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;

namespace FSTRaK.ViewModels
{
    internal class StatisticsViewModel : BaseViewModel
    {
        private List<string> _aircraftTypes;
        public List<string> AircraftTypes
        {
            get => _aircraftTypes;
            set
            {
                _aircraftTypes = value;
                OnPropertyChanged();
            }
        }

        private List<string> _airlines;
        public List<string> Airlines
        {
            get => _airlines;
            set
            {
                _airlines = value;
                OnPropertyChanged();
            }
        }


        private string _airlineFilter;

        public string AirlineFilter
        {
            get => _airlineFilter;
            set
            {
                _airlineFilter = value;
                UpdateStatistics();
                OnPropertyChanged();
            }
        }


        private string _aircraftTypeFilter;

        public string AircraftTypeFilter { get => _aircraftTypeFilter; 
            set
            {
                _aircraftTypeFilter = value;
                UpdateStatistics();
                OnPropertyChanged();
            }
        }


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
            CreateFilters();
            UpdateStatistics();
        }

        private void CreateFilters()
        {
            using var logbookContext = new LogbookContext();
            Airlines = logbookContext.Flights.Select(f => f.Aircraft.Airline).Where(at => !at.Trim().Equals(String.Empty)).Distinct().ToList();
            AircraftTypes = logbookContext.Flights.Select(f => f.Aircraft.AircraftType).Where(at => !at.Trim().Equals(String.Empty)).Distinct().ToList();

            Airlines.Add(string.Empty);
            AircraftTypes.Add(string.Empty);
            Airlines.Sort();
            AircraftTypes.Sort();
        }

        private void UpdateStatistics()
        {
            using var logbookContext = new LogbookContext();
            var flights = logbookContext.Flights
                .Select(f => f)
                .OrderByDescending(f => f.Id)
                .Include(f => f.Aircraft);

            if (!string.IsNullOrEmpty(AirlineFilter))
            {
                flights = flights.Where(f => f.Aircraft.Airline.Equals(AirlineFilter));
            }

            if (!string.IsNullOrEmpty(AircraftTypeFilter))
            {
                flights = flights.Where(f => f.Aircraft.AircraftType.Equals(AircraftTypeFilter));
            }

            TotalNumberOfFlights = flights.Count();
            if (TotalNumberOfFlights == 0)
            {
                TotalFlightTime = "";
                AvgFlightTime = "";

                TotalFlightDistance = "";
                AvgFlightDistance = "";

                TotalFuelUsed = "";
                AvgFuelUsed = "";

                TotalPayload = "";
                AvgPayload = "";
                return;
            }
            
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

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using System.Linq.Dynamic;
using Serilog;
using System.Windows.Media.Media3D;
using FSTRaK.DataTypes;

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

        private string _avgLandingFpm;
        public string AvgLandingFpm
        {
            get => _avgLandingFpm;
            set
            {
                _avgLandingFpm = value;
                OnPropertyChanged();
            }
        }

        private string _minLandingFpm;
        public string MinLandingFpm
        {
            get => _minLandingFpm;
            set
            {
                _minLandingFpm = value;
                OnPropertyChanged();
            }
        }

        private string _maxLandingFpm;
        public string MaxLandingFpm
        {
            get => _maxLandingFpm;
            set
            {
                _maxLandingFpm = value;
                OnPropertyChanged();
            }
        }

        private Dictionary<string, double> _aircraftDistribution;
        public Dictionary<string, double> AircraftDistribution
        {
            get => _aircraftDistribution;
            set
            {
                _aircraftDistribution = value;
                OnPropertyChanged();
            }
        }

        private Dictionary<string, double> _airlineDistribution;
        public Dictionary<string, double> AirlineDistribution
        {
            get => _airlineDistribution;
            set
            {
                _airlineDistribution = value;
                OnPropertyChanged();
            }
        }

        private Dictionary<string, double> _frequentDepartureAirportsDistribution;
        public Dictionary<string, double> FrequentDepartureAirportsDistribution
        {
            get => _frequentDepartureAirportsDistribution;
            set
            {
                _frequentDepartureAirportsDistribution = value;
                OnPropertyChanged();
            }
        }

        private Dictionary<string, double> _frequentArrivalAirportsDistribution;
        public Dictionary<string, double> FrequentArrivalAirportsDistribution
        {
            get => _frequentArrivalAirportsDistribution;
            set
            {
                _frequentArrivalAirportsDistribution = value;
                OnPropertyChanged();
            }
        }

        private Dictionary<DateTime, double> _flightsPerDay;
        public Dictionary<DateTime, double> FlightsPerDay
        {
            get => _flightsPerDay;
            set
            {
                _flightsPerDay = value;
                OnPropertyChanged();
            }
        }

        private TimePeriod _timePeriod = TimePeriod.Day;
        public TimePeriod TimePeriod
        {
            get => _timePeriod;
            set
            {
                _timePeriod = value;
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
                .Include(f => f.Aircraft).ToList();

            if (!string.IsNullOrEmpty(AirlineFilter))
            {
                flights = flights.Where(f => f.Aircraft.Airline.Equals(AirlineFilter)).ToList();
            }

            if (!string.IsNullOrEmpty(AircraftTypeFilter))
            {
                flights = flights.Where(f => f.Aircraft.AircraftType.Equals(AircraftTypeFilter)).ToList();
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

            AvgLandingFpm = $"{flights.Where(f => f.LandingFpm != null).Average(f => f.LandingFpm):N0}";
            MinLandingFpm = $"{flights.Where(f => f.LandingFpm != null).Max(f => f.LandingFpm):N0}";
            MaxLandingFpm = $"{flights.Where(f => f.LandingFpm != null).Min(f => f.LandingFpm):N0}";

            AircraftDistribution = CalculateAircraftDistribution(flights);

            AirlineDistribution = CalculateAirlineDistribution(flights);

            FrequentDepartureAirportsDistribution = CalculateAirportDistribution(flights, AirportType.DEP);

            FrequentArrivalAirportsDistribution = CalculateAirportDistribution(flights, AirportType.ARR);

            FlightsPerDay = CalculateFlightsPerDay(flights);
        }

        private static Dictionary<DateTime, double> CalculateFlightsPerDay(List<Flight> flights)
        {
            return flights
                .GroupBy(f => f.StartTime.Date) 
                .ToDictionary(g => g.Key, g => Convert.ToDouble(g.Count()));
        }

        private static Dictionary<DateTime, int> CalculateFlightsPerMonth(List<Flight> flights)
        {
            // TODO
            return flights
                .GroupBy(f => f.StartTime.Date)
                .ToDictionary(g => g.Key, g => g.Count());
        }


        private static Dictionary<string, double> CalculateAirlineDistribution(List<Flight> flights)
        {
            var airlineDistribution = new Dictionary<string, double>();
            var i = 0;
            var sum = 0;
            foreach (var f in flights.GroupBy(f => f.Aircraft.Airline)
                         .Select(group => new
                         {
                             airline = group.Key,
                             count = group.Count()
                         })
                         .OrderByDescending(x => x.count))
            {
                if (i < 5)
                {
                    airlineDistribution.Add(f.airline.Equals(string.Empty) ? "None" : f.airline, f.count);
                    i++;
                }
                else
                {
                    sum += f.count;
                }
            }

            if (sum > 0)
            {
                airlineDistribution.Add("Other", (double)sum);
            }

            return airlineDistribution;
        }

        private static Dictionary<string, double> CalculateAircraftDistribution(List<Flight> flights)
        {
            var i = 0;
            var sum = 0;
            var aircraftDistribution = new Dictionary<string, double>();
            foreach (var f in flights.GroupBy(f => f.Aircraft.AircraftType)
                         .Select(group => new
                         {
                             aircraftType = group.Key,
                             count = group.Count()
                         })
                         .OrderByDescending(x => x.count))
            {
                if (i < 5)
                {
                    aircraftDistribution.Add(f.aircraftType, f.count);
                    i++;
                }
                else
                {
                    sum += f.count;
                }
            }

            if (sum > 0)
            {
                aircraftDistribution.Add("Other", (double)sum);
            }

            return aircraftDistribution;
        }

        private static Dictionary<string, double> CalculateAirportDistribution(List<Flight> flights, AirportType type)
        {
            var i = 0;
            var sum = 0;
            var airportDistribution = new Dictionary<string, double>();

            if (type == AirportType.ARR)
            {
                foreach (var f in flights.GroupBy(f => f.ArrivalAirport)
                             .Select(group => new
                             {
                                 airport = group.Key,
                                 count = group.Count()
                             })
                             .OrderByDescending(x => x.count))
                {
                    if (i < 5)
                    {
                        if (f.airport != null)
                        {
                            airportDistribution.Add(f.airport, f.count);
                        }
                    }
                    else
                    {
                        sum += f.count;
                    }
                    i++;
                }

                if (i >= 5 && sum > 0)
                {
                    airportDistribution.Add("Other", (double)sum);
                }
            }
            else
            {
                foreach (var f in flights.GroupBy(f => f.DepartureAirport)
                             .Select(group => new
                             {
                                 airport = group.Key,
                                 count = group.Count()
                             })
                             .OrderByDescending(x => x.count))
                {
                    if (i < 5)
                    {
                        if (f.airport != null)
                        {
                            airportDistribution.Add(f.airport, f.count);
                        }
                    }
                    else
                    {
                        sum += f.count;
                    }
                    i++;
                }

                if (i >= 5 && sum > 0)
                {
                    airportDistribution.Add("Other", (double)sum);
                }
            }

            return airportDistribution;
        }

        private enum AirportType
        {
            DEP, ARR
        }

        internal void ViewLoaded()
        {
            UpdateStatistics();
        }
    }
}

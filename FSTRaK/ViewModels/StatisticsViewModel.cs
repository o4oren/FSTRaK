using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using System.Linq.Dynamic;
using Serilog;
using System.Windows.Media.Media3D;
using FSTRaK.DataTypes;
using Newtonsoft.Json;

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
                DebounceUpdateStatistics();
                OnPropertyChanged();
            }
        }

        private string _aircraftTypeFilter;

        public string AircraftTypeFilter
        {
            get => _aircraftTypeFilter;
            set
            {
                _aircraftTypeFilter = value;
                DebounceUpdateStatistics();
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

        // Debounce fields
        private CancellationTokenSource _debounceCts;
        private readonly object _debounceLock = new();
        private const int DebounceMilliseconds = 300;

        // Cache filename (per-user local data)
        private readonly string _filtersCachePath = Path.Combine(PathUtil.GetApplicationLocalDataPath(), "filters_cache.json");

        public StatisticsViewModel()
        {
            // removed synchronous CreateFilters() call to avoid startup blocking.
        }

        /// <summary>
        /// Read cached filter lists from disk (fast). Returns null if cache not present or invalid.
        /// </summary>
        private (List<string> airlines, List<string> types)? TryReadFiltersCache()
        {
            try
            {
                if (!File.Exists(_filtersCachePath)) return null;
                var json = File.ReadAllText(_filtersCachePath);
                var dto = JsonConvert.DeserializeObject<FiltersCacheDto>(json);
                if (dto?.Airlines == null || dto?.AircraftTypes == null) return null;
                return (dto.Airlines, dto.AircraftTypes);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to read filters cache");
                return null;
            }
        }

        /// <summary>
        /// Persist filter lists to disk. Best-effort; failures are logged but not fatal.
        /// </summary>
        private void WriteFiltersCache(List<string> airlines, List<string> types)
        {
            try
            {
                var dto = new FiltersCacheDto { Airlines = airlines, AircraftTypes = types, Generated = DateTime.UtcNow };
                var json = JsonConvert.SerializeObject(dto);
                var dir = Path.GetDirectoryName(_filtersCachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_filtersCachePath, json);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to write filters cache");
            }
        }

        private class FiltersCacheDto
        {
            public List<string> Airlines { get; set; }
            public List<string> AircraftTypes { get; set; }
            public DateTime Generated { get; set; }
        }

        /// <summary>
        /// CreateFiltersAsync returns immediately if a cache exists (fast), then refreshes cache in background.
        /// If no cache exists it will query DB and create cache.
        /// </summary>
        private async Task CreateFiltersAsync()
        {
            try
            {
                // Try read cache first - fast path
                var cached = TryReadFiltersCache();
                if (cached != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Airlines = cached.Value.airlines;
                        AircraftTypes = cached.Value.types;
                    });

                    // Refresh cache in background without blocking UI
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var fresh = await QueryFiltersFromDbAsync().ConfigureAwait(false);
                            if (!Enumerable.SequenceEqual(fresh.airlines, cached.Value.airlines) ||
                                !Enumerable.SequenceEqual(fresh.types, cached.Value.types))
                            {
                                WriteFiltersCache(fresh.airlines, fresh.types);
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    Airlines = fresh.airlines;
                                    AircraftTypes = fresh.types;
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Background refresh of filters failed");
                        }
                    });

                    return;
                }

                // No cache found - query DB and cache results (this runs async and won't block UI)
                var result = await QueryFiltersFromDbAsync().ConfigureAwait(false);
                WriteFiltersCache(result.airlines, result.types);
                App.Current.Dispatcher.Invoke(() =>
                {
                    Airlines = result.airlines;
                    AircraftTypes = result.types;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CreateFiltersAsync failed");
            }
        }

        /// <summary>
        /// Query distinct Airlines and AircraftTypes from the Aircraft table.
        /// Runs off the UI thread when awaited with ConfigureAwait(false).
        /// </summary>
        private async Task<(List<string> airlines, List<string> types)> QueryFiltersFromDbAsync()
        {
            using var logbookContext = new LogbookContext();

            var airlinesTask = logbookContext.Aircraft
                .AsNoTracking()
                .Where(a => a.Airline != null && a.Airline.Trim() != "")
                .Select(a => a.Airline)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var typesTask = logbookContext.Aircraft
                .AsNoTracking()
                .Where(a => a.AircraftType != null && a.AircraftType.Trim() != "")
                .Select(a => a.AircraftType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            await Task.WhenAll(airlinesTask, typesTask).ConfigureAwait(false);

            var airlines = airlinesTask.Result;
            var types = typesTask.Result;

            // ensure empty selection exists
            if (!airlines.Contains(string.Empty)) airlines.Add(string.Empty);
            if (!types.Contains(string.Empty)) types.Add(string.Empty);

            return (airlines, types);
        }

        /// <summary>
        /// Debounce wrapper. Cancels any pending update and schedules a new one after DebounceMilliseconds.
        /// </summary>
        private void DebounceUpdateStatistics()
        {
            lock (_debounceLock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                // fire-and-forget background task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;
                        await UpdateStatisticsAsync().ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // expected when another change occurs
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Debounced UpdateStatistics failed");
                    }
                }, token);
            }
        }

        /// <summary>
        /// Async server-side implementation: applies filters to the IQueryable before materializing.
        /// Uses EF6's ToListAsync to avoid blocking the UI thread while the DB work runs.
        /// </summary>
        private async Task UpdateStatisticsAsync()
        {
            try
            {
                using var logbookContext = new LogbookContext();

                IQueryable<Flight> query = logbookContext.Flights
                    .AsNoTracking()
                    .Include(f => f.Aircraft);

                if (!string.IsNullOrEmpty(AirlineFilter))
                {
                    query = query.Where(f => f.Aircraft.Airline == AirlineFilter);
                }

                if (!string.IsNullOrEmpty(AircraftTypeFilter))
                {
                    query = query.Where(f => f.Aircraft.AircraftType == AircraftTypeFilter);
                }

                var flights = await query.OrderByDescending(f => f.Id).ToListAsync().ConfigureAwait(false);

                // compute aggregates on background thread then marshal updates to UI
                var totalNumberOfFlights = flights.Count;
                if (totalNumberOfFlights == 0)
                {
                    // marshal empty results to UI
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TotalNumberOfFlights = 0;
                        TotalFlightTime = "";
                        AvgFlightTime = "";
                        TotalFlightDistance = "";
                        AvgFlightDistance = "";
                        TotalFuelUsed = "";
                        AvgFuelUsed = "";
                        TotalPayload = "";
                        AvgPayload = "";
                        AircraftDistribution = new Dictionary<string, double>();
                        AirlineDistribution = new Dictionary<string, double>();
                        FrequentDepartureAirportsDistribution = new Dictionary<string, double>();
                        FrequentArrivalAirportsDistribution = new Dictionary<string, double>();
                        FlightsPerDay = new Dictionary<DateTime, double>();
                    });
                    return;
                }

                var totalFlightMilis = flights.Sum(f => f.FlightTimeMilis);
                var averageFlightMilis = flights.Average(f => f.FlightTimeMilis);
                var totalFlightTimeTs = TimeSpan.FromTicks(totalFlightMilis);
                var avgFlightTimeTs = TimeSpan.FromTicks((long)averageFlightMilis);

                var totalFlightDistance = flights.Sum(f => f.FlightDistanceNm);
                var avgFlightDistance = flights.Average(f => f.FlightDistanceNm);

                var totalFuel = flights.Sum(f => f.TotalFuelUsed);
                var avgFuel = flights.Average(f => f.TotalFuelUsed);

                var totalPayload = flights.Sum(f => f.TotalPayloadLbs ?? 0);
                var avgPayload = flights.Average(f => f.TotalPayloadLbs ?? 0);

                var avgLandingFpm = flights.Where(f => f.LandingFpm != null).Average(f => f.LandingFpm);
                var minLandingFpm = flights.Where(f => f.LandingFpm != null).Max(f => f.LandingFpm);
                var maxLandingFpm = flights.Where(f => f.LandingFpm != null).Min(f => f.LandingFpm);

                var aircraftDist = CalculateAircraftDistribution(flights);
                var airlineDist = CalculateAirlineDistribution(flights);
                var depDist = CalculateAirportDistribution(flights, AirportType.DEP);
                var arrDist = CalculateAirportDistribution(flights, AirportType.ARR);
                var flightsPerDay = CalculateFlightsPerDay(flights);

                // update UI-bound properties on the UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    TotalNumberOfFlights = totalNumberOfFlights;
                    TotalFlightTime = $"{(int)totalFlightTimeTs.TotalHours}:{totalFlightTimeTs.Minutes}:{totalFlightTimeTs.Seconds}";
                    AvgFlightTime = $"{(int)avgFlightTimeTs.TotalHours}:{avgFlightTimeTs.Minutes}:{avgFlightTimeTs.Seconds}";
                    TotalFlightDistance = $"{totalFlightDistance:N1}";
                    AvgFlightDistance = $"{avgFlightDistance:N1}";
                    TotalFuelUsed = UnitsUtil.GetWeightString(totalFuel);
                    AvgFuelUsed = UnitsUtil.GetWeightString(avgFuel);
                    TotalPayload = UnitsUtil.GetWeightString(totalPayload);
                    AvgPayload = UnitsUtil.GetWeightString(avgPayload);
                    AvgLandingFpm = $"{avgLandingFpm:N0}";
                    MinLandingFpm = $"{minLandingFpm:N0}";
                    MaxLandingFpm = $"{maxLandingFpm:N0}";

                    AircraftDistribution = aircraftDist;
                    AirlineDistribution = airlineDist;
                    FrequentDepartureAirportsDistribution = depDist;
                    FrequentArrivalAirportsDistribution = arrDist;
                    FlightsPerDay = flightsPerDay;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "UpdateStatisticsAsync failed");
            }
        }

        private static Dictionary<DateTime, double> CalculateFlightsPerDay(List<Flight> flights)
        {
            return flights
                .GroupBy(f => f.StartTime.Date)
                .ToDictionary(g => g.Key, g => Convert.ToDouble(g.Count()));
        }

        private static Dictionary<DateTime, int> CalculateFlightsPerMonth(List<Flight> flights)
        {
            return flights
                .GroupBy(f => new { f.StartTime.Year, f.StartTime.Month })
                .ToDictionary(g => new DateTime(g.Key.Year, g.Key.Month, 1), g => g.Count());
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
            // start filter population and stats when the view actually loads
            _ = CreateFiltersAsync();
            _ = UpdateStatisticsAsync(); // perform async update when view loads
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using FSTRaK.Utils;
using Serilog;
using FSTRaK.DataTypes;
using Newtonsoft.Json;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MapControl;
using SkiaSharp;

namespace FSTRaK.ViewModels
{
    internal class StatisticsViewModel : BaseViewModel
    {
        // ── Filter option lists (reactive, interdependent) ──────────────────

        private ObservableCollection<string> _filteredAirlines = new ObservableCollection<string>();
        public ObservableCollection<string> FilteredAirlines
        {
            get => _filteredAirlines;
            set { _filteredAirlines = value; OnPropertyChanged(); }
        }

        private ObservableCollection<string> _filteredAircraftTypes = new ObservableCollection<string>();
        public ObservableCollection<string> FilteredAircraftTypes
        {
            get => _filteredAircraftTypes;
            set { _filteredAircraftTypes = value; OnPropertyChanged(); }
        }

        private ObservableCollection<string> _filteredTailNumbers = new ObservableCollection<string>();
        public ObservableCollection<string> FilteredTailNumbers
        {
            get => _filteredTailNumbers;
            set { _filteredTailNumbers = value; OnPropertyChanged(); }
        }

        // ── Active filter values ─────────────────────────────────────────────

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

        private string _tailNumberFilter;
        public string TailNumberFilter
        {
            get => _tailNumberFilter;
            set
            {
                _tailNumberFilter = value;
                DebounceUpdateStatistics();
                OnPropertyChanged();
            }
        }

        // ── Summary stat properties ──────────────────────────────────────────

        private int _totalNumberOfFlights;
        public int TotalNumberOfFlights
        {
            get => _totalNumberOfFlights;
            set { _totalNumberOfFlights = value; OnPropertyChanged(); }
        }

        private string _totalFlightTime;
        public string TotalFlightTime
        {
            get => _totalFlightTime;
            set { _totalFlightTime = value; OnPropertyChanged(); }
        }

        private string _avgFlightTime;
        public string AvgFlightTime
        {
            get => _avgFlightTime;
            set { _avgFlightTime = value; OnPropertyChanged(); }
        }

        private string _totalFlightDistance;
        public string TotalFlightDistance
        {
            get => _totalFlightDistance;
            set { _totalFlightDistance = value; OnPropertyChanged(); }
        }

        private string _totalFuelUsed;
        public string TotalFuelUsed
        {
            get => _totalFuelUsed;
            set { _totalFuelUsed = value; OnPropertyChanged(); }
        }

        private string _totalPayload;
        public string TotalPayload
        {
            get => _totalPayload;
            set { _totalPayload = value; OnPropertyChanged(); }
        }

        private string _avgLandingFpm;
        public string AvgLandingFpm
        {
            get => _avgLandingFpm;
            set { _avgLandingFpm = value; OnPropertyChanged(); }
        }

        // ── FlightsPerDay (kept for TimePeriod rebuild) ──────────────────────

        private Dictionary<DateTime, double> _flightsPerDay;
        public Dictionary<DateTime, double> FlightsPerDay
        {
            get => _flightsPerDay;
            set { _flightsPerDay = value; OnPropertyChanged(); }
        }

        private TimePeriod _timePeriod = TimePeriod.Day;
        public TimePeriod TimePeriod
        {
            get => _timePeriod;
            set
            {
                _timePeriod = value;
                OnPropertyChanged();
                if (_flightsPerDay != null && _flightsPerDay.Count > 0)
                {
                    var (series, xAxes) = BuildFlightsPerPeriod(_flightsPerDay, _timePeriod);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        FlightsPerPeriodSeries = series;
                        FlightsPerPeriodXAxes = xAxes;
                    });
                }
            }
        }

        // ── Route map data ───────────────────────────────────────────────────

        private List<(Location dep, Location arr)> _flightRoutes;
        public List<(Location dep, Location arr)> FlightRoutes
        {
            get => _flightRoutes;
            set { _flightRoutes = value; OnPropertyChanged(); }
        }

        // ── LiveCharts2 series properties ────────────────────────────────────

        private ISeries[] _flightsPerPeriodSeries = Array.Empty<ISeries>();
        public ISeries[] FlightsPerPeriodSeries
        {
            get => _flightsPerPeriodSeries;
            set { _flightsPerPeriodSeries = value; OnPropertyChanged(); }
        }

        private Axis[] _flightsPerPeriodXAxes = Array.Empty<Axis>();
        public Axis[] FlightsPerPeriodXAxes
        {
            get => _flightsPerPeriodXAxes;
            set { _flightsPerPeriodXAxes = value; OnPropertyChanged(); }
        }

        private Axis[] _flightsPerPeriodYAxes = new[] { new Axis() };
        public Axis[] FlightsPerPeriodYAxes
        {
            get => _flightsPerPeriodYAxes;
            set { _flightsPerPeriodYAxes = value; OnPropertyChanged(); }
        }

        private ISeries[] _depAirportsSeries = Array.Empty<ISeries>();
        public ISeries[] DepAirportsSeries
        {
            get => _depAirportsSeries;
            set { _depAirportsSeries = value; OnPropertyChanged(); }
        }

        private ISeries[] _arrAirportsSeries = Array.Empty<ISeries>();
        public ISeries[] ArrAirportsSeries
        {
            get => _arrAirportsSeries;
            set { _arrAirportsSeries = value; OnPropertyChanged(); }
        }

        private ISeries[] _aircraftTypesSeries = Array.Empty<ISeries>();
        public ISeries[] AircraftTypesSeries
        {
            get => _aircraftTypesSeries;
            set { _aircraftTypesSeries = value; OnPropertyChanged(); }
        }

        private ISeries[] _airlinesSeries = Array.Empty<ISeries>();
        public ISeries[] AirlinesSeries
        {
            get => _airlinesSeries;
            set { _airlinesSeries = value; OnPropertyChanged(); }
        }

        private ISeries[] _landingRateSeries = Array.Empty<ISeries>();
        public ISeries[] LandingRateSeries
        {
            get => _landingRateSeries;
            set { _landingRateSeries = value; OnPropertyChanged(); }
        }

        private Axis[] _landingRateXAxes = Array.Empty<Axis>();
        public Axis[] LandingRateXAxes
        {
            get => _landingRateXAxes;
            set { _landingRateXAxes = value; OnPropertyChanged(); }
        }

        private Axis[] _landingRateYAxes = new[] { new Axis() };
        public Axis[] LandingRateYAxes
        {
            get => _landingRateYAxes;
            set { _landingRateYAxes = value; OnPropertyChanged(); }
        }

        private ISeries[] _countriesSeries = Array.Empty<ISeries>();
        public ISeries[] CountriesSeries
        {
            get => _countriesSeries;
            set { _countriesSeries = value; OnPropertyChanged(); }
        }

        // ── Debounce ─────────────────────────────────────────────────────────

        private CancellationTokenSource _debounceCts;
        private readonly object _debounceLock = new object();
        private const int DebounceMilliseconds = 300;

        // ── Filter cache ─────────────────────────────────────────────────────

        private readonly string _filtersCachePath = Path.Combine(PathUtil.GetApplicationLocalDataPath(), "filters_cache.json");

        public StatisticsViewModel()
        {
        }

        // ── Cache helpers ─────────────────────────────────────────────────────

        private (List<string> airlines, List<string> types, List<string> tailNumbers)? TryReadFiltersCache()
        {
            try
            {
                if (!File.Exists(_filtersCachePath)) return null;
                var json = File.ReadAllText(_filtersCachePath);
                var dto = JsonConvert.DeserializeObject<FiltersCacheDto>(json);
                if (dto?.Airlines == null || dto?.AircraftTypes == null || dto?.TailNumbers == null) return null;
                return (dto.Airlines, dto.AircraftTypes, dto.TailNumbers);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to read filters cache");
                return null;
            }
        }

        private void WriteFiltersCache(List<string> airlines, List<string> types, List<string> tailNumbers)
        {
            try
            {
                var dto = new FiltersCacheDto { Airlines = airlines, AircraftTypes = types, TailNumbers = tailNumbers, Generated = DateTime.UtcNow };
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
            public List<string> TailNumbers { get; set; }
            public DateTime Generated { get; set; }
        }

        // ── Filter loading ────────────────────────────────────────────────────

        private async Task CreateFiltersAsync()
        {
            try
            {
                var cached = TryReadFiltersCache();
                if (cached != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        FilteredAirlines = new ObservableCollection<string>(cached.Value.airlines);
                        FilteredAircraftTypes = new ObservableCollection<string>(cached.Value.types);
                        FilteredTailNumbers = new ObservableCollection<string>(cached.Value.tailNumbers);
                    });

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var fresh = await QueryFiltersFromDbAsync().ConfigureAwait(false);
                            WriteFiltersCache(fresh.airlines, fresh.types, fresh.tailNumbers);
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                FilteredAirlines = new ObservableCollection<string>(fresh.airlines);
                                FilteredAircraftTypes = new ObservableCollection<string>(fresh.types);
                                FilteredTailNumbers = new ObservableCollection<string>(fresh.tailNumbers);
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Background refresh of filters failed");
                        }
                    });

                    return;
                }

                var result = await QueryFiltersFromDbAsync().ConfigureAwait(false);
                WriteFiltersCache(result.airlines, result.types, result.tailNumbers);
                App.Current.Dispatcher.Invoke(() =>
                {
                    FilteredAirlines = new ObservableCollection<string>(result.airlines);
                    FilteredAircraftTypes = new ObservableCollection<string>(result.types);
                    FilteredTailNumbers = new ObservableCollection<string>(result.tailNumbers);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CreateFiltersAsync failed");
            }
        }

        /// <summary>
        /// Query distinct filter options respecting current active filters for interdependency.
        /// </summary>
        private async Task<(List<string> airlines, List<string> types, List<string> tailNumbers)> QueryFiltersFromDbAsync()
        {
            using (var logbookContext = new LogbookContext())
            {
                IQueryable<Aircraft> query = logbookContext.Aircraft.AsNoTracking();

                if (!string.IsNullOrEmpty(AirlineFilter))
                    query = query.Where(a => a.Airline == AirlineFilter);
                if (!string.IsNullOrEmpty(AircraftTypeFilter))
                    query = query.Where(a => a.AircraftType == AircraftTypeFilter);
                if (!string.IsNullOrEmpty(TailNumberFilter))
                    query = query.Where(a => a.TailNumber == TailNumberFilter);

                var airlinesTask = query
                    .Where(a => a.Airline != null && a.Airline.Trim() != "")
                    .Select(a => a.Airline).Distinct().OrderBy(a => a).ToListAsync();

                var typesTask = query
                    .Where(a => a.AircraftType != null && a.AircraftType.Trim() != "")
                    .Select(a => a.AircraftType).Distinct().OrderBy(t => t).ToListAsync();

                var tailsTask = query
                    .Where(a => a.TailNumber != null && a.TailNumber.Trim() != "")
                    .Select(a => a.TailNumber).Distinct().OrderBy(t => t).ToListAsync();

                await Task.WhenAll(airlinesTask, typesTask, tailsTask).ConfigureAwait(false);

                var airlines = airlinesTask.Result;
                if (!airlines.Contains(string.Empty)) airlines.Insert(0, string.Empty);

                var types = typesTask.Result;
                if (!types.Contains(string.Empty)) types.Insert(0, string.Empty);

                var tails = tailsTask.Result;
                if (!tails.Contains(string.Empty)) tails.Insert(0, string.Empty);

                return (airlines, types, tails);
            }
        }

        // ── Debounce ──────────────────────────────────────────────────────────

        private void DebounceUpdateStatistics()
        {
            lock (_debounceLock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(DebounceMilliseconds, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;
                        await UpdateStatisticsAsync().ConfigureAwait(false);
                        await CreateFiltersAsync().ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        // expected
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Debounced UpdateStatistics failed");
                    }
                }, token);
            }
        }

        // ── Main data update ──────────────────────────────────────────────────

        private async Task UpdateStatisticsAsync()
        {
            try
            {
                using (var logbookContext = new LogbookContext())
                {
                    IQueryable<Flight> query = logbookContext.Flights
                        .AsNoTracking()
                        .Include(f => f.Aircraft);

                    if (!string.IsNullOrEmpty(AirlineFilter))
                        query = query.Where(f => f.Aircraft.Airline == AirlineFilter);
                    if (!string.IsNullOrEmpty(AircraftTypeFilter))
                        query = query.Where(f => f.Aircraft.AircraftType == AircraftTypeFilter);
                    if (!string.IsNullOrEmpty(TailNumberFilter))
                        query = query.Where(f => f.Aircraft.TailNumber == TailNumberFilter);

                    var flights = await query.OrderByDescending(f => f.Id).ToListAsync().ConfigureAwait(false);

                    var totalNumberOfFlights = flights.Count;
                    if (totalNumberOfFlights == 0)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            TotalNumberOfFlights = 0;
                            TotalFlightTime = "";
                            AvgFlightTime = "";
                            TotalFlightDistance = "";
                            TotalFuelUsed = "";
                            TotalPayload = "";
                            AvgLandingFpm = "";
                            FlightsPerDay = new Dictionary<DateTime, double>();
                            FlightRoutes = new List<(Location, Location)>();
                            FlightsPerPeriodSeries = Array.Empty<ISeries>();
                            FlightsPerPeriodXAxes = Array.Empty<Axis>();
                            DepAirportsSeries = Array.Empty<ISeries>();
                            ArrAirportsSeries = Array.Empty<ISeries>();
                            AircraftTypesSeries = Array.Empty<ISeries>();
                            AirlinesSeries = Array.Empty<ISeries>();
                            LandingRateSeries = Array.Empty<ISeries>();
                            LandingRateXAxes = Array.Empty<Axis>();
                            CountriesSeries = Array.Empty<ISeries>();
                        });
                        return;
                    }

                    var totalFlightMilis = flights.Sum(f => f.FlightTimeMilis);
                    var averageFlightMilis = flights.Average(f => f.FlightTimeMilis);
                    var totalFlightTimeTs = TimeSpan.FromTicks(totalFlightMilis);
                    var avgFlightTimeTs = TimeSpan.FromTicks((long)averageFlightMilis);

                    var totalFlightDistance = flights.Sum(f => f.FlightDistanceNm);
                    var totalFuel = flights.Sum(f => f.TotalFuelUsed);
                    var totalPayload = flights.Sum(f => f.TotalPayloadLbs ?? 0);

                    var avgLandingFpm = flights.Where(f => f.LandingFpm != null).Any()
                        ? flights.Where(f => f.LandingFpm != null).Average(f => f.LandingFpm)
                        : (double?)null;

                    var aircraftDist = CalculateAircraftDistribution(flights);
                    var airlineDist = CalculateAirlineDistribution(flights);
                    var depDist = CalculateAirportDistribution(flights, AirportType.DEP);
                    var arrDist = CalculateAirportDistribution(flights, AirportType.ARR);
                    var flightsPerDay = CalculateFlightsPerDay(flights);
                    var landingDist = CalculateLandingRateDistribution(flights);
                    var countryDist = CalculateCountryDistribution(flights);
                    var flightRoutes = CalculateFlightRoutes(flights);

                    var (fpSeries, fpXAxes) = BuildFlightsPerPeriod(flightsPerDay, _timePeriod);
                    var (lrSeries, lrXAxes) = BuildLandingRateSeries(landingDist);

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TotalNumberOfFlights = totalNumberOfFlights;
                        TotalFlightTime = $"{(int)totalFlightTimeTs.TotalHours}:{totalFlightTimeTs.Minutes:D2}:{totalFlightTimeTs.Seconds:D2}";
                        AvgFlightTime = $"{(int)avgFlightTimeTs.TotalHours}:{avgFlightTimeTs.Minutes:D2}:{avgFlightTimeTs.Seconds:D2}";
                        TotalFlightDistance = $"{totalFlightDistance:N1}";
                        TotalFuelUsed = UnitsUtil.GetWeightString(totalFuel);
                        TotalPayload = UnitsUtil.GetWeightString(totalPayload);
                        AvgLandingFpm = avgLandingFpm.HasValue ? $"{avgLandingFpm:N0}" : "-";

                        FlightsPerDay = flightsPerDay;
                        FlightRoutes = flightRoutes;

                        FlightsPerPeriodSeries = fpSeries;
                        FlightsPerPeriodXAxes = fpXAxes;
                        DepAirportsSeries = BuildPieSeries(depDist);
                        ArrAirportsSeries = BuildPieSeries(arrDist);
                        AircraftTypesSeries = BuildPieSeries(aircraftDist);
                        AirlinesSeries = BuildPieSeries(airlineDist);
                        LandingRateSeries = lrSeries;
                        LandingRateXAxes = lrXAxes;
                        CountriesSeries = BuildPieSeries(countryDist);
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "UpdateStatisticsAsync failed");
            }
        }

        // ── Calculation helpers ───────────────────────────────────────────────

        private static Dictionary<DateTime, double> CalculateFlightsPerDay(List<Flight> flights)
        {
            return flights
                .GroupBy(f => f.StartTime.Date)
                .ToDictionary(g => g.Key, g => Convert.ToDouble(g.Count()));
        }

        private static Dictionary<string, double> CalculateAirlineDistribution(List<Flight> flights)
        {
            var dist = new Dictionary<string, double>();
            var i = 0;
            var sum = 0;
            foreach (var f in flights.GroupBy(f => f.Aircraft.Airline)
                         .Select(g => new { airline = g.Key, count = g.Count() })
                         .OrderByDescending(x => x.count))
            {
                if (i < 5)
                {
                    dist.Add(string.IsNullOrEmpty(f.airline) ? "None" : f.airline, f.count);
                    i++;
                }
                else
                {
                    sum += f.count;
                }
            }
            if (sum > 0) dist.Add("Other", (double)sum);
            return dist;
        }

        private static Dictionary<string, double> CalculateAircraftDistribution(List<Flight> flights)
        {
            var dist = new Dictionary<string, double>();
            var i = 0;
            var sum = 0;
            foreach (var f in flights.GroupBy(f => f.Aircraft.AircraftType)
                         .Select(g => new { aircraftType = g.Key, count = g.Count() })
                         .OrderByDescending(x => x.count))
            {
                if (i < 5)
                {
                    dist.Add(string.IsNullOrEmpty(f.aircraftType) ? "Unknown" : f.aircraftType, f.count);
                    i++;
                }
                else
                {
                    sum += f.count;
                }
            }
            if (sum > 0) dist.Add("Other", (double)sum);
            return dist;
        }

        private static Dictionary<string, double> CalculateAirportDistribution(List<Flight> flights, AirportType type)
        {
            var dist = new Dictionary<string, double>();
            var i = 0;
            var sum = 0;
            var groups = type == AirportType.ARR
                ? flights.GroupBy(f => f.ArrivalAirport)
                : flights.GroupBy(f => f.DepartureAirport);

            foreach (var g in groups.Select(gr => new { airport = gr.Key, count = gr.Count() })
                         .OrderByDescending(x => x.count))
            {
                if (i < 5)
                {
                    if (!string.IsNullOrEmpty(g.airport))
                        dist.Add(g.airport, g.count);
                }
                else
                {
                    sum += g.count;
                }
                i++;
            }
            if (i >= 5 && sum > 0) dist.Add("Other", (double)sum);
            return dist;
        }

        private static Dictionary<string, double> CalculateCountryDistribution(List<Flight> flights)
        {
            var dist = new Dictionary<string, double>();
            var i = 0;
            var sum = 0;
            foreach (var f in flights
                         .GroupBy(f => f.DepartureAirportDetails?.iso_country ?? "Unknown")
                         .Select(g => new { country = g.Key, count = g.Count() })
                         .OrderByDescending(x => x.count))
            {
                if (i < 5)
                {
                    dist.Add(string.IsNullOrEmpty(f.country) ? "Unknown" : f.country, f.count);
                    i++;
                }
                else
                {
                    sum += f.count;
                }
            }
            if (sum > 0) dist.Add("Other", (double)sum);
            return dist;
        }

        private static List<(double bucketCenter, int count)> CalculateLandingRateDistribution(List<Flight> flights)
        {
            const int bucketSize = 50;
            const int minFpm = -1000;
            const int maxFpm = 0;

            var buckets = new Dictionary<int, int>();
            for (int b = minFpm; b < maxFpm; b += bucketSize)
                buckets[b] = 0;

            foreach (var f in flights.Where(f => f.LandingFpm.HasValue))
            {
                var fpm = (int)f.LandingFpm.Value;
                var bucket = (int)(Math.Floor((double)fpm / bucketSize) * bucketSize);
                bucket = Math.Max(minFpm, Math.Min(maxFpm - bucketSize, bucket));
                if (buckets.ContainsKey(bucket))
                    buckets[bucket]++;
            }

            return buckets
                .OrderBy(kv => kv.Key)
                .Select(kv => (bucketCenter: (double)(kv.Key + bucketSize / 2), count: kv.Value))
                .ToList();
        }

        private static List<(Location dep, Location arr)> CalculateFlightRoutes(List<Flight> flights)
        {
            var routes = new List<(Location, Location)>();
            foreach (var f in flights)
            {
                var dep = f.DepartureAirportDetails;
                var arr = f.ArrivalAirportDetails;
                if (dep == null || arr == null) continue;
                if (dep.latitude_deg == 0 && dep.longitude_deg == 0) continue;
                if (arr.latitude_deg == 0 && arr.longitude_deg == 0) continue;
                routes.Add((
                    new Location(dep.latitude_deg, dep.longitude_deg),
                    new Location(arr.latitude_deg, arr.longitude_deg)
                ));
            }
            return routes;
        }

        // ── LiveCharts2 series builders ───────────────────────────────────────

        private static readonly SKColor[] ChartPalette = new[]
        {
            SKColor.Parse("#4FC3F7"),
            SKColor.Parse("#81C784"),
            SKColor.Parse("#FFB74D"),
            SKColor.Parse("#F06292"),
            SKColor.Parse("#CE93D8"),
            SKColor.Parse("#80DEEA")
        };

        private static ISeries[] BuildPieSeries(Dictionary<string, double> data)
        {
            return data.Select((kv, i) => (ISeries)new PieSeries<double>
            {
                Name = kv.Key,
                Values = new double[] { kv.Value },
                Fill = new SolidColorPaint(ChartPalette[i % ChartPalette.Length])
            }).ToArray();
        }

        private static (ISeries[] series, Axis[] xAxes) BuildFlightsPerPeriod(
            Dictionary<DateTime, double> flightsPerDay, TimePeriod period)
        {
            Dictionary<DateTime, double> data;
            string labelFormat;

            if (period == TimePeriod.Month)
            {
                data = flightsPerDay
                    .GroupBy(x => new DateTime(x.Key.Year, x.Key.Month, 1))
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
                labelFormat = "MMM yyyy";
            }
            else if (period == TimePeriod.Year)
            {
                data = flightsPerDay
                    .GroupBy(x => new DateTime(x.Key.Year, 1, 1))
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
                labelFormat = "yyyy";
            }
            else
            {
                data = flightsPerDay;
                labelFormat = "dd/MM/yy";
            }

            var ordered = data.OrderBy(kv => kv.Key).ToList();
            var values = ordered.Select(kv => kv.Value).ToArray();
            var labels = ordered.Select(kv => kv.Key.ToString(labelFormat)).ToArray();

            var series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = values,
                    Fill = new SolidColorPaint(SKColor.Parse("#4FC3F7")),
                    Name = "Flights"
                }
            };

            var xAxes = new[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = -45,
                    TextSize = 10
                }
            };

            return (series, xAxes);
        }

        private static (ISeries[] series, Axis[] xAxes) BuildLandingRateSeries(
            List<(double bucketCenter, int count)> dist)
        {
            var values = dist.Select(d => (double)d.count).ToArray();
            var labels = dist.Select(d => $"{(int)d.bucketCenter}").ToArray();

            var series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = values,
                    Fill = new SolidColorPaint(SKColor.Parse("#81C784")),
                    Name = "Landings"
                }
            };

            var xAxes = new[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = -45,
                    TextSize = 10,
                    Name = "fpm"
                }
            };

            return (series, xAxes);
        }

        private enum AirportType { DEP, ARR }

        internal void ViewLoaded()
        {
            _ = CreateFiltersAsync();
            _ = UpdateStatisticsAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.DataTypes;
using FSTRaK.Utils;
using MapControl;
using static FSTRaK.ViewModels.LiveViewViewModel;

namespace FSTRaK.ViewModels
{
    internal enum ClientType { Pilot, AirportATC, CtrATC }

    internal class SelectedClientViewModel : BaseViewModel
    {
        // ── Identity
        public NetworkType Network { get; }
        public ClientType ClientKind { get; }   // named ClientKind to avoid collision with enum name
        public string Callsign { get; }
        public bool IsOwnAircraft { get; }
        public bool IsOwnAircraftInFlight { get; }

        // ── Track
        internal bool IvaoTrackFetched;    // true once IVAO API track has been loaded
        internal bool VatsimTrackFetched;  // true once StatSim track has been loaded
        private List<TrackPoint> _trackPoints = new List<TrackPoint>();
        public List<TrackPoint> TrackPoints
        {
            get => _trackPoints;
            set { _trackPoints = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrackLocations)); }
        }
        public IEnumerable<Location> TrackLocations =>
            _trackPoints.Select(t => new Location(t.Latitude, t.Longitude));

        // Extended track including geodesic prefix from departure to first known point (VATSIM)
        private List<Location> _extendedTrackLocations;
        public IEnumerable<Location> EffectiveTrackLocations =>
            _extendedTrackLocations ?? TrackLocations;

        private IEnumerable<Location> _destLine;
        public IEnumerable<Location> DestinationLine
        {
            get => _destLine;
            set { _destLine = value; OnPropertyChanged(); }
        }

        // ── Network identity helpers (for XAML visibility toggling)
        public bool IsVatsim => Network == NetworkType.Vatsim;
        public bool IsIvao   => Network == NetworkType.Ivao;

        // ── Pilot display properties
        public bool IsPilot => ClientKind == ClientType.Pilot;
        public string TrackStrokeColor => Network == NetworkType.Ivao ? "#FFFF8C00" : "#FF38bdf8";
        public string PilotName { get; private set; }
        public int? CidInt { get; }
        public string CidDisplay => CidInt?.ToString() ?? "";
        public string FlightRules { get; private set; }
        public string AircraftType { get; private set; }
        public string Departure { get; }
        public string Arrival { get; }
        public int Altitude { get; private set; }
        public int Groundspeed { get; private set; }
        public int Heading { get; private set; }
        public string Squawk { get; private set; }
        public string CruiseAlt { get; private set; }
        public string OnlineTime { get; private set; }
        public string RouteString { get; private set; }
        public string Remarks { get; private set; }
        public double ProgressPercent { get; private set; }
        public string EtaDisplay { get; private set; }
        public string RemainingNmDisplay { get; private set; }

        // ── ATC display properties
        public bool IsAirportATC => ClientKind == ClientType.AirportATC;
        public bool IsCtrATC => ClientKind == ClientType.CtrATC;
        public string AirportName { get; }
        public string FacilityLabel { get; }
        public string Frequency { get; }
        public string RatingDisplay { get; private set; }
        public string VisualRange { get; }
        public List<AtcControllerRow> Controllers { get; } = new List<AtcControllerRow>();
        public string AtisText { get; private set; }

        // Raw references
        public VatsimAicraft VatsimPilotItem { get; private set; }
        public IvaoAircraft IvaoPilotItem { get; private set; }
        public VatsimControlledAirport VatsimAirportItem { get; private set; }
        public IvaoAtcItem IvaoAtcItemRef { get; private set; }

        // ── VATSIM pilot constructor
        public SelectedClientViewModel(VatsimAicraft item, bool isOwn, bool isOwnInFlight, List<TrackPoint> tracks)
        {
            Network = NetworkType.Vatsim;
            ClientKind = ClientType.Pilot;
            VatsimPilotItem = item;
            IsOwnAircraft = isOwn;
            IsOwnAircraftInFlight = isOwnInFlight;
            Callsign = item.Pilot.callsign;
            PilotName = item.Pilot.name;
            CidInt = item.Pilot.cid;
            AircraftType = item.Pilot.flight_plan?.aircraft_short ?? "";
            Departure = item.Pilot.flight_plan?.departure ?? "";
            Arrival = item.Pilot.flight_plan?.arrival ?? "";
            FlightRules = item.Pilot.flight_plan?.flight_rules == "V" ? "VFR" : "IFR";
            Altitude = item.Pilot.altitude;
            Groundspeed = item.Pilot.groundspeed;
            Heading = item.Pilot.heading;
            Squawk = item.Pilot.flight_plan?.assigned_transponder ?? "";
            CruiseAlt = item.Pilot.flight_plan?.altitude ?? "";
            RouteString = item.Pilot.flight_plan?.route ?? "";
            Remarks = item.Pilot.flight_plan?.remarks ?? "";
            OnlineTime = "";
            _trackPoints = tracks;
        }

        // ── IVAO pilot constructor
        public SelectedClientViewModel(IvaoAircraft item, bool isOwn, bool isOwnInFlight, List<TrackPoint> tracks)
        {
            Network = NetworkType.Ivao;
            ClientKind = ClientType.Pilot;
            IvaoPilotItem = item;
            IsOwnAircraft = isOwn;
            IsOwnAircraftInFlight = isOwnInFlight;
            Callsign = item.Callsign;
            PilotName = "";
            CidInt = item.Pilot.userId;
            AircraftType = item.Pilot.flightPlan?.aircraftId ?? "";
            Departure = item.Pilot.flightPlan?.departureId ?? "";
            Arrival = item.Pilot.flightPlan?.arrivalId ?? "";
            FlightRules = "IFR";
            Altitude = item.Pilot.lastTrack.altitude;
            Groundspeed = item.Pilot.lastTrack.groundSpeed;
            Heading = item.Pilot.lastTrack.heading;
            Squawk = "";
            CruiseAlt = "";
            RouteString = "";
            Remarks = "";
            OnlineTime = "";
            _trackPoints = tracks;
        }

        // ── VATSIM airport ATC constructor
        public SelectedClientViewModel(VatsimControlledAirport item)
        {
            Network = NetworkType.Vatsim;
            ClientKind = ClientType.AirportATC;
            VatsimAirportItem = item;
            Callsign = item.Airport?.ICAO ?? "";
            AirportName = item.Airport?.Name ?? "";
            FacilityLabel = BuildFacilityLabel(item.Controllers);
            Frequency = item.Controllers?.FirstOrDefault()?.frequency ?? "";
            RatingDisplay = "";
            VisualRange = "";
            AtisText = BuildAtisText(item.Atis);
            if (item.Controllers != null)
                foreach (var c in item.Controllers)
                    Controllers.Add(new AtcControllerRow(
                        c.callsign,
                        MapVatsimFacility(c.facility),
                        c.frequency,
                        MapVatsimRating(c.rating),
                        c.name ?? "",
                        FormatOnlineTime(c.logon_time)));
        }

        // ── VATSIM FIR constructor
        public SelectedClientViewModel(VatsimControlledFir item)
        {
            Network = NetworkType.Vatsim;
            ClientKind = ClientType.CtrATC;
            Callsign = item.Name ?? "";
            AirportName = item.Name ?? "";
            FacilityLabel = "FIR";
            Frequency = item.Controllers?.FirstOrDefault()?.frequency ?? "";
            RatingDisplay = "";
            VisualRange = "";
            AtisText = null;
            if (item.Controllers != null)
                foreach (var c in item.Controllers)
                    Controllers.Add(new AtcControllerRow(
                        c.callsign,
                        MapVatsimFacility(c.facility),
                        c.frequency,
                        MapVatsimRating(c.rating),
                        c.name ?? "",
                        FormatOnlineTime(c.logon_time)));
        }

        // ── VATSIM UIR constructor (UIR is a merged polygon — show the single primary controller)
        public SelectedClientViewModel(VatsimControlledUir item)
        {
            Network = NetworkType.Vatsim;
            ClientKind = ClientType.CtrATC;
            Callsign = item.Callsign ?? "";
            AirportName = item.Callsign ?? "";
            FacilityLabel = "UIR";
            var primary = item.Controllers?.FirstOrDefault();
            Frequency = primary?.frequency ?? "";
            RatingDisplay = primary != null ? MapVatsimRating(primary.rating) : "";
            VisualRange = "";
            AtisText = null;
            if (primary != null)
                Controllers.Add(new AtcControllerRow(
                    primary.callsign,
                    MapVatsimFacility(primary.facility),
                    primary.frequency,
                    MapVatsimRating(primary.rating),
                    primary.name ?? "",
                    FormatOnlineTime(primary.logon_time)));
        }

        // ── IVAO ATC static factory (replaces two constructors with `when` clauses)
        public static SelectedClientViewModel FromIvaoAtc(IvaoAtcItem item)
        {
            return new SelectedClientViewModel(item, item.IsCtr);
        }

        private SelectedClientViewModel(IvaoAtcItem item, bool isCtr)
        {
            Network = NetworkType.Ivao;
            IvaoAtcItemRef = item;
            Callsign = item.Callsign;
            AirportName = "";
            RatingDisplay = "";
            VisualRange = "";
            AtisText = null;

            if (isCtr)
            {
                ClientKind = ClientType.CtrATC;
                FacilityLabel = "CTR";
                Frequency = item.SingleEntry?.atcSession?.frequency.ToString("F3") ?? "";
                if (item.SingleEntry != null)
                    Controllers.Add(new AtcControllerRow(item.Callsign, "CTR", Frequency, "", ""));
            }
            else
            {
                ClientKind = ClientType.AirportATC;
                FacilityLabel = BuildIvaoFacilityLabel(item.AtcEntries);
                Frequency = item.AtcEntries?.FirstOrDefault()?.atcSession?.frequency.ToString("F3") ?? "";
                if (item.AtcEntries != null)
                    foreach (var e in item.AtcEntries)
                        Controllers.Add(new AtcControllerRow(
                            e.callsign,
                            e.atcSession?.position ?? "",
                            e.atcSession?.frequency.ToString("F3") ?? "",
                            "", ""));
            }
        }

        // ── IVAO enrichment — called after API fetch completes
        public void EnrichIvaoPilot(string pilotName, string flightRules, string aircraftType,
            string route, string remarks, string cruiseAlt, string squawk, string onlineTime)
        {
            PilotName = pilotName;
            FlightRules = flightRules;
            AircraftType = aircraftType;
            RouteString = route;
            Remarks = remarks;
            CruiseAlt = cruiseAlt;
            Squawk = squawk;
            OnlineTime = onlineTime;
            OnPropertyChanged(nameof(PilotName));
            OnPropertyChanged(nameof(FlightRules));
            OnPropertyChanged(nameof(AircraftType));
            OnPropertyChanged(nameof(RouteString));
            OnPropertyChanged(nameof(Remarks));
            OnPropertyChanged(nameof(CruiseAlt));
            OnPropertyChanged(nameof(Squawk));
            OnPropertyChanged(nameof(OnlineTime));
        }

        public void EnrichIvaoAtc(string controllerName, string ratingDisplay, string onlineTime, string atisText)
        {
            // Update the first controller row with enriched name/rating
            if (Controllers.Count > 0)
            {
                var c = Controllers[0];
                Controllers[0] = new AtcControllerRow(c.Callsign, c.Position, c.Frequency, ratingDisplay, controllerName, onlineTime);
            }
            RatingDisplay = ratingDisplay;
            OnlineTime = onlineTime;
            AtisText = atisText;
            OnPropertyChanged(nameof(RatingDisplay));
            OnPropertyChanged(nameof(OnlineTime));
            OnPropertyChanged(nameof(AtisText));
            OnPropertyChanged(nameof(Controllers));
        }

        // ── Live update methods
        public void UpdateFromVatsimPilot(VatsimAicraft item)
        {
            VatsimPilotItem = item;
            Altitude = item.Pilot.altitude;
            Groundspeed = item.Pilot.groundspeed;
            Heading = item.Pilot.heading;
            OnPropertyChanged(nameof(Altitude));
            OnPropertyChanged(nameof(Groundspeed));
            OnPropertyChanged(nameof(Heading));
            RecalcProgress();
        }

        public void UpdateFromIvaoPilot(IvaoAircraft item)
        {
            IvaoPilotItem = item;
            Altitude = item.Pilot.lastTrack.altitude;
            Groundspeed = item.Pilot.lastTrack.groundSpeed;
            Heading = item.Pilot.lastTrack.heading;
            OnPropertyChanged(nameof(Altitude));
            OnPropertyChanged(nameof(Groundspeed));
            OnPropertyChanged(nameof(Heading));
            RecalcProgress();
        }

        // ── Airport coordinates and progress
        private double _depLat, _depLon, _arrLat, _arrLon;
        private bool _hasAirportCoords;

        public void SetAirportCoordinates(double depLat, double depLon, double arrLat, double arrLon)
        {
            _depLat = depLat; _depLon = depLon;
            _arrLat = arrLat; _arrLon = arrLon;
            _hasAirportCoords = true;
            RecalcProgress();
        }

        public void RecalcProgress()
        {
            if (!_hasAirportCoords) return;

            double currentLat = IvaoPilotItem?.Pilot.lastTrack.latitude ?? VatsimPilotItem?.Pilot.latitude ?? 0;
            double currentLon = IvaoPilotItem?.Pilot.lastTrack.longitude ?? VatsimPilotItem?.Pilot.longitude ?? 0;

            // Always update the destination line regardless of speed
            DestinationLine = GeodesicUtil.Interpolate(currentLat, currentLon, _arrLat, _arrLon);

            if (_trackPoints.Count == 0)
            {
                // No track loaded (no key, key not set, or fetch failed/empty) — geodesic dep→current as fallback
                var depToCurrent = GeodesicUtil.Interpolate(_depLat, _depLon, currentLat, currentLon);
                _extendedTrackLocations = depToCurrent.Count > 1 ? depToCurrent : null;
            }
            else
            {
                _extendedTrackLocations = null;
            }

            double totalNm = GeodesicUtil.DistanceNm(_depLat, _depLon, _arrLat, _arrLon);
            if (totalNm < 1 || Groundspeed <= 0) return;

            double flownNm = GeodesicUtil.DistanceNm(_depLat, _depLon, currentLat, currentLon);
            double remainingNm = GeodesicUtil.DistanceNm(currentLat, currentLon, _arrLat, _arrLon);

            ProgressPercent = Math.Min(100, Math.Round(flownNm / totalNm * 100, 1));
            RemainingNmDisplay = $"{(int)remainingNm} nm";

            double hoursRemaining = remainingNm / Math.Max(1, Groundspeed);
            var eta = TimeSpan.FromHours(hoursRemaining);
            EtaDisplay = eta.TotalHours >= 1
                ? $"{(int)eta.TotalHours}h {eta.Minutes:D2}m"
                : $"{eta.Minutes}m";

            OnPropertyChanged(nameof(ProgressPercent));
            OnPropertyChanged(nameof(EtaDisplay));
            OnPropertyChanged(nameof(RemainingNmDisplay));
        }

        // ── Static helpers
        private static string MapVatsimFacility(int facility) => facility switch
        {
            0 => "OBS", 1 => "FSS", 2 => "DEL", 3 => "GND",
            4 => "TWR", 5 => "APP", 6 => "CTR", _ => ""
        };

        // Controller.rating is a string in the VATSIM model; parse it before mapping
        private static string MapVatsimRating(string ratingStr)
        {
            if (!int.TryParse(ratingStr, out int rating)) return "";
            return rating switch
            {
                1 => "OBS", 2 => "S1", 3 => "S2", 4 => "S3",
                5 => "C1", 7 => "C3", 8 => "I1", 10 => "I3",
                11 => "SUP", 12 => "ADM", _ => ""
            };
        }

        internal static string FormatOnlineTime(DateTime logonUtc)
        {
            var elapsed = DateTime.UtcNow - logonUtc;
            return elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m"
                : $"{elapsed.Minutes}m";
        }

        internal static string FormatOnlineTime(string logonTime)
        {
            if (string.IsNullOrEmpty(logonTime)) return "";
            if (!DateTime.TryParse(logonTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) return "";
            return FormatOnlineTime(dt);
        }

        private static string BuildAtisText(IEnumerable<Atis> atisList)
        {
            if (atisList == null) return null;
            var entries = atisList.Where(a => a.text_atis != null).ToList();
            if (entries.Count == 0) return null;
            if (entries.Count == 1)
                return string.Join("\n", entries[0].text_atis);
            return string.Join("\n\n", entries.Select(a =>
                $"{a.callsign}:\n{string.Join("\n", a.text_atis)}"));
        }

        private static string BuildFacilityLabel(IEnumerable<Controller> controllers)
        {
            if (controllers == null) return "";
            var labels = controllers.Select(c => MapVatsimFacility(c.facility))
                                    .Where(l => !string.IsNullOrEmpty(l))
                                    .Distinct().ToList();
            return string.Join(" · ", labels);
        }

        private static string BuildIvaoFacilityLabel(IEnumerable<IvaoAtcEntry> entries)
        {
            if (entries == null) return "";
            return string.Join(" · ", entries
                .Select(e => e.atcSession?.position ?? "")
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct());
        }
    }

    internal record AtcControllerRow(string Callsign, string Position, string Frequency, string Rating, string Name, string OnlineTime = "");
}

# Client Detail Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a glassmorphism detail popup panel, styled hover tooltips, and flight path overlays for clicked pilots and ATC on the FSTRaK live map.

**Architecture:** A `SelectedClientViewModel` wrapper class holds the selected pilot/ATC + computed display data. `LiveViewViewModel` owns a `SelectedClient` property that is set by click commands on map markers and drives a `ClientDetailPanelControl` UserControl anchored to the bottom-right of `LiveView.xaml`. Flight track points are accumulated per-callsign during IVAO/VATSIM polling; IVAO additionally fetches full session history on selection. Two `MapPolyline` bindings (solid trail + dashed geodesic) render when a pilot is selected.

**Tech Stack:** WPF (.NET Framework 4.7.2), C#, MapControl.WPF v13.4, Entity Framework 6 / SQLite (no changes), Serilog (no changes), `System.Windows.Input.ICommand` / `RelayCommand`, `MapControl.Location` / `LocationCollection` / `MapPolyline`

---

## File Map

### New files
- `FSTRaK/ViewModels/SelectedClientViewModel.cs` — wrapper for selected pilot or ATC; all display properties
- `FSTRaK/Utils/GeodesicUtil.cs` — great-circle interpolation for dashed destination line
- `FSTRaK/Utils/AirlineLogoResolver.cs` — maps 3-letter ICAO prefix → BitmapImage from Assets/AirlineLogos/
- `FSTRaK/Views/ClientDetailPanelControl.xaml` + `.cs` — glassmorphism UserControl for detail panel
- `FSTRaK/Assets/AirlineLogos/` — PNG files (sourced from VatView's `/app/assets/logos/`)
- `FSTRaK/Assets/NetworkLogos/vatsim.png` + `ivao.png` — small network brand logos

### Modified files
- `FSTRaK/ViewModels/LiveViewViewModel.cs` — add `SelectedClient`, track accumulation dict, click commands, own-aircraft suppression, IVAO track fetch
- `FSTRaK/Views/LiveView.xaml` — add click commands to marker ItemTemplates, add `ClientDetailPanelControl` overlay, add selected-track `MapPolyline` bindings, restyle `ToolTip` templates
- `FSTRaK/Views/LiveView.xaml.cs` — add Escape key handler
- `FSTRaK/Utils/CoordinatesUtil.cs` — no changes needed (GeodesicUtil is separate)

---

## Task 1: TrackPoint record and track accumulation dictionary

**Files:**
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs`

- [ ] **Step 1: Add `TrackPoint` record and accumulation dictionary to `LiveViewViewModel`**

Open `LiveViewViewModel.cs`. At the top of the class (after the existing field declarations, around line 22), add:

```csharp
// Flight track accumulation
public record TrackPoint(double Latitude, double Longitude, int Altitude, DateTime Timestamp);
private readonly Dictionary<string, List<TrackPoint>> _pilotTracks = new();
```

- [ ] **Step 2: Append track point in `ProcessVatsimPilots`**

Find `ProcessVatsimPilots()` (around line 867). After `newVatsimAircraftList.Add(aircraft);`, add:

```csharp
var key = $"VATSIM:{pilot.callsign}";
if (!_pilotTracks.ContainsKey(key))
    _pilotTracks[key] = new List<TrackPoint>();
_pilotTracks[key].Add(new TrackPoint(pilot.latitude, pilot.longitude, pilot.altitude, DateTime.UtcNow));
```

Also add cleanup after `VatsimAircraftList.ReplaceContent(newVatsimAircraftList);`:

```csharp
// Remove tracks for pilots no longer in feed
var activeVatsimKeys = newVatsimAircraftList.Select(a => $"VATSIM:{a.Pilot.callsign}").ToHashSet();
foreach (var key in _pilotTracks.Keys.Where(k => k.StartsWith("VATSIM:") && !activeVatsimKeys.Contains(k)).ToList())
    _pilotTracks.Remove(key);
```

- [ ] **Step 3: Append track point in `ProcessIvaoPilots`**

Find `ProcessIvaoPilots()` (around line 659). Inside the `foreach (var pilot in data.pilots)` loop, after `newList.Add(new IvaoAircraft(pilot));`, add:

```csharp
var key = $"IVAO:{pilot.callsign}";
if (!_pilotTracks.ContainsKey(key))
    _pilotTracks[key] = new List<TrackPoint>();
_pilotTracks[key].Add(new TrackPoint(pilot.lastTrack.latitude, pilot.lastTrack.longitude, pilot.lastTrack.altitude, DateTime.UtcNow));
```

After `IvaoAircraftList.ReplaceContent(newList);`, add:

```csharp
var activeIvaoKeys = newList.Select(a => $"IVAO:{a.Callsign}").ToHashSet();
foreach (var key in _pilotTracks.Keys.Where(k => k.StartsWith("IVAO:") && !activeIvaoKeys.Contains(k)).ToList())
    _pilotTracks.Remove(key);
```

Note: `IvaoAircraft` doesn't currently expose `Callsign` — that is added in Task 2.

- [ ] **Step 4: Build the project to confirm it compiles**

Open the solution in Visual Studio and build `x64|Debug`. Expected: 0 errors (the `IvaoAircraft.Callsign` reference will produce one error that is fixed in Task 2 — that is fine, fix it in Task 2).

- [ ] **Step 5: Commit**

```
git add FSTRaK/ViewModels/LiveViewViewModel.cs
git commit -m "feat: add per-pilot track accumulation dictionary"
```

---

## Task 2: Expose missing properties on wrapper classes

**Files:**
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs` (inner classes `IvaoAircraft`, `VatsimAicraft`, `IvaoAtcItem`, `VatsimControlledAirport`)

The inner classes currently lack CID, Callsign, network, and pilot reference on IVAO — needed by `SelectedClientViewModel` and commands.

- [ ] **Step 1: Add `Callsign` and `Pilot` reference to `IvaoAircraft`**

Find the `IvaoAircraft` inner class (around line 1227). Add two properties and store the pilot:

```csharp
internal class IvaoAircraft
{
    public IvaoPilot Pilot { get; }          // add this
    public string Callsign { get; }          // add this
    public Location Location { get; set; }
    public double Heading { get; set; }
    public string Icon { get; set; }
    public string TooltipText { get; set; }

    public IvaoAircraft(IvaoPilot pilot)
    {
        Pilot = pilot;                        // add this
        Callsign = pilot.callsign;            // add this
        Location = new Location(pilot.lastTrack.latitude, pilot.lastTrack.longitude);
        Heading = pilot.lastTrack.heading;
        Icon = AircraftResolver.GetAircraftIcon(pilot.flightPlan?.aircraftId ?? "").Item1;
        var departure = pilot.flightPlan?.departureId ?? "";
        var destination = pilot.flightPlan?.arrivalId ?? "";
        var aircraft = pilot.flightPlan?.aircraftId ?? "";
        TooltipText = $"{pilot.callsign}\n{departure} → {destination}\n{aircraft}\nALT: {pilot.lastTrack.altitude}  GS: {pilot.lastTrack.groundSpeed}";
    }
}
```

- [ ] **Step 2: Add `Callsign` and `AtcEntries` to `IvaoAtcItem`**

Find the `IvaoAtcItem` inner class (around line 1247). Add:

```csharp
public string Callsign { get; }
public List<IvaoAtcEntry> AtcEntries { get; }   // for airport group
public IvaoAtcEntry SingleEntry { get; }         // for CTR/subcenter
```

In the group constructor (takes `List<IvaoAtcEntry> entries`), add at the top:
```csharp
Callsign = entries[0].atcPosition.airportId;
AtcEntries = entries;
```

In the single-entry constructor (takes `IvaoAtcEntry entry`), add at the top:
```csharp
Callsign = entry.callsign;
SingleEntry = entry;
```

- [ ] **Step 3: Add `Callsign` and `Controllers`+`AtisControllers` to `VatsimControlledAirport`**

The class already has `Airport`, `Controllers`, and `Atis`. Just expose a `Callsign` convenience property:

```csharp
public string Callsign => Airport?.icao ?? "";
```

- [ ] **Step 4: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add FSTRaK/ViewModels/LiveViewViewModel.cs
git commit -m "feat: expose Callsign and raw data references on map wrapper classes"
```

---

## Task 3: `GeodesicUtil` — great-circle interpolation

**Files:**
- Create: `FSTRaK/Utils/GeodesicUtil.cs`

- [ ] **Step 1: Create `GeodesicUtil.cs`**

```csharp
using System;
using System.Collections.Generic;
using MapControl;

namespace FSTRaK.Utils
{
    public static class GeodesicUtil
    {
        private const double EarthRadiusNm = 3440.065;

        /// <summary>
        /// Returns a series of lat/lon points along the great-circle path from
        /// (startLat, startLon) to (endLat, endLon), with approximately one point
        /// every <paramref name="stepNm"/> nautical miles.
        /// </summary>
        public static List<Location> Interpolate(
            double startLat, double startLon,
            double endLat, double endLon,
            double stepNm = 50.0)
        {
            var points = new List<Location>();
            double lat1 = ToRad(startLat), lon1 = ToRad(startLon);
            double lat2 = ToRad(endLat),  lon2 = ToRad(endLon);

            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double centralAngle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double totalNm = centralAngle * EarthRadiusNm;

            int steps = Math.Max(2, (int)(totalNm / stepNm));
            for (int i = 0; i <= steps; i++)
            {
                double f = (double)i / steps;
                double sinD = Math.Sin(centralAngle);
                if (Math.Abs(sinD) < 1e-10)
                {
                    points.Add(new Location(startLat, startLon));
                    continue;
                }
                double A = Math.Sin((1 - f) * centralAngle) / sinD;
                double B = Math.Sin(f * centralAngle) / sinD;
                double x = A * Math.Cos(lat1) * Math.Cos(lon1) + B * Math.Cos(lat2) * Math.Cos(lon2);
                double y = A * Math.Cos(lat1) * Math.Sin(lon1) + B * Math.Cos(lat2) * Math.Sin(lon2);
                double z = A * Math.Sin(lat1) + B * Math.Sin(lat2);
                double lat = Math.Atan2(z, Math.Sqrt(x * x + y * y));
                double lon = Math.Atan2(y, x);
                points.Add(new Location(ToDeg(lat), ToDeg(lon)));
            }
            return points;
        }

        /// <summary>Distance in nautical miles between two lat/lon points.</summary>
        public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            double r1 = ToRad(lat1), r2 = ToRad(lat2);
            double dLat = r2 - r1;
            double dLon = ToRad(lon2) - ToRad(lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(r1) * Math.Cos(r2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)) * EarthRadiusNm;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
        private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
    }
}
```

- [ ] **Step 2: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add FSTRaK/Utils/GeodesicUtil.cs
git commit -m "feat: add GeodesicUtil for great-circle interpolation"
```

---

## Task 4: `AirlineLogoResolver`

**Files:**
- Create: `FSTRaK/Utils/AirlineLogoResolver.cs`
- Create: `FSTRaK/Assets/AirlineLogos/` (populated manually — see step 1)

- [ ] **Step 1: Copy airline logo PNGs from VatView**

Copy all PNG/JPG files from `/Users/ogeva/IdeaProjects/VatView/app/assets/logos/` into `FSTRaK/FSTRaK/Assets/AirlineLogos/`. In Visual Studio, add them to the project with Build Action = `Resource`. (This step is manual — do it once, not per airline.)

- [ ] **Step 2: Copy network logo PNGs**

Place small (32×32) VATSIM and IVAO logos as:
- `FSTRaK/Assets/NetworkLogos/vatsim.png`
- `FSTRaK/Assets/NetworkLogos/ivao.png`

These can be sourced from the official VATSIM/IVAO websites or the VatView assets. Add to project with Build Action = `Resource`.

- [ ] **Step 3: Create `AirlineLogoResolver.cs`**

```csharp
using System;
using System.Collections.Concurrent;
using System.Windows.Media.Imaging;

namespace FSTRaK.Utils
{
    public static class AirlineLogoResolver
    {
        private static readonly ConcurrentDictionary<string, BitmapImage?> _cache = new();

        /// <summary>
        /// Returns a BitmapImage for the airline identified by the first 3 characters of
        /// <paramref name="callsign"/>, or null if no logo is available.
        /// </summary>
        public static BitmapImage? GetLogo(string callsign)
        {
            if (string.IsNullOrWhiteSpace(callsign) || callsign.Length < 3) return null;
            var prefix = callsign.Substring(0, 3).ToUpperInvariant();
            return _cache.GetOrAdd(prefix, TryLoad);
        }

        /// <summary>Returns the network logo BitmapImage for VATSIM or IVAO.</summary>
        public static BitmapImage GetNetworkLogo(NetworkType network)
        {
            var key = network == NetworkType.VATSIM ? "vatsim" : "ivao";
            return _cache.GetOrAdd(key, _ =>
            {
                var uri = new Uri($"pack://application:,,,/FSTRaK;component/Assets/NetworkLogos/{key}.png");
                return LoadFromUri(uri);
            })!;
        }

        private static BitmapImage? TryLoad(string prefix)
        {
            var uri = new Uri($"pack://application:,,,/FSTRaK;component/Assets/AirlineLogos/{prefix}.png");
            try { return LoadFromUri(uri); }
            catch { return null; }
        }

        private static BitmapImage LoadFromUri(Uri uri)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = uri;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
    }
}
```

Note: `NetworkType` is defined in Task 5.

- [ ] **Step 4: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors (after `NetworkType` is defined in Task 5 — if building before Task 5, temporarily use a string parameter).

- [ ] **Step 5: Commit**

```
git add FSTRaK/Utils/AirlineLogoResolver.cs FSTRaK/Assets/
git commit -m "feat: add AirlineLogoResolver and logo assets"
```

---

## Task 5: `SelectedClientViewModel`

**Files:**
- Create: `FSTRaK/ViewModels/SelectedClientViewModel.cs`

This class wraps a selected pilot or ATC item and exposes all display properties for binding. It also holds `TrackPoints`.

- [ ] **Step 1: Create `SelectedClientViewModel.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.Utils;
using MapControl;
using static FSTRaK.ViewModels.LiveViewViewModel;

namespace FSTRaK.ViewModels
{
    public enum NetworkType { VATSIM, IVAO }
    public enum ClientType  { Pilot, AirportATC, CtrATC }

    public class SelectedClientViewModel : BaseViewModel
    {
        // ── Identity ────────────────────────────────────────────────────────
        public NetworkType Network { get; }
        public ClientType  ClientType { get; }
        public string Callsign { get; }
        public bool IsOwnAircraft { get; }
        public bool IsOwnAircraftInFlight { get; }

        // ── Track ────────────────────────────────────────────────────────────
        private List<TrackPoint> _trackPoints = new();
        public List<TrackPoint> TrackPoints
        {
            get => _trackPoints;
            set { _trackPoints = value; OnPropertyChanged(); OnPropertyChanged(nameof(TrackLocations)); }
        }
        public IEnumerable<Location> TrackLocations =>
            _trackPoints.Select(t => new Location(t.Latitude, t.Longitude));

        // Geodesic destination line (set by LiveViewViewModel after airport lookup)
        private IEnumerable<Location>? _destLine;
        public IEnumerable<Location>? DestinationLine
        {
            get => _destLine;
            set { _destLine = value; OnPropertyChanged(); }
        }

        // ── Logos ────────────────────────────────────────────────────────────
        public BitmapImage NetworkLogo => AirlineLogoResolver.GetNetworkLogo(Network);
        public BitmapImage? AirlineLogo => ClientType == ClientType.Pilot
            ? AirlineLogoResolver.GetLogo(Callsign) : null;

        // ── Pilot display properties ─────────────────────────────────────────
        public bool IsPilot => ClientType == ClientType.Pilot;
        public string? PilotName { get; }
        public int? CidInt { get; }
        public string CidDisplay => CidInt?.ToString() ?? "";
        public string FlightRules { get; }     // "IFR" or "VFR"
        public string AircraftType { get; }    // e.g. "B738"
        public string Departure { get; }
        public string Arrival { get; }
        public string? DepartureName { get; }  // set later by airport lookup if needed
        public string? ArrivalName { get; }
        public int Altitude { get; private set; }
        public int Groundspeed { get; private set; }
        public int Heading { get; private set; }
        public string Squawk { get; }
        public string CruiseAlt { get; }
        public string OnlineTime { get; private set; }
        public string RouteString { get; }
        public string Remarks { get; }
        // ETA / progress (computed when track is set or on update)
        public double ProgressPercent { get; private set; }
        public string EtaDisplay { get; private set; }
        public string RemainingNmDisplay { get; private set; }

        // ── ATC display properties ───────────────────────────────────────────
        public bool IsAirportATC => ClientType == ClientType.AirportATC;
        public bool IsCtrATC     => ClientType == ClientType.CtrATC;
        public string AirportName { get; }     // for airport ATC
        public string FacilityLabel { get; }   // e.g. "CTR", "APP"
        public string Frequency { get; }
        public string RatingDisplay { get; }
        public string VisualRange { get; }
        public List<AtcControllerRow> Controllers { get; } = new();
        public string? AtisText { get; }

        // Raw references (for live-update matching)
        public VatsimAicraft? VatsimPilotItem { get; private set; }
        public IvaoAircraft? IvaoPilotItem { get; private set; }
        public VatsimControlledAirport? VatsimAirportItem { get; private set; }
        public IvaoAtcItem? IvaoAtcItemRef { get; private set; }

        // ── Constructors ─────────────────────────────────────────────────────

        /// VATSIM pilot
        public SelectedClientViewModel(VatsimAicraft item, bool isOwn, bool isOwnInFlight, List<TrackPoint> tracks)
        {
            Network = NetworkType.VATSIM;
            ClientType = ClientType.Pilot;
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
            OnlineTime = "";   // logon_time not in current VATSIM model; leave blank
            _trackPoints = tracks;
            UpdateProgress();
        }

        /// IVAO pilot
        public SelectedClientViewModel(IvaoAircraft item, bool isOwn, bool isOwnInFlight, List<TrackPoint> tracks)
        {
            Network = NetworkType.IVAO;
            ClientType = ClientType.Pilot;
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
            UpdateProgress();
        }

        /// VATSIM airport ATC
        public SelectedClientViewModel(VatsimControlledAirport item)
        {
            Network = NetworkType.VATSIM;
            ClientType = ClientType.AirportATC;
            VatsimAirportItem = item;
            Callsign = item.Airport?.icao ?? "";
            AirportName = item.Airport?.name ?? "";
            FacilityLabel = BuildFacilityLabel(item.Controllers);
            Frequency = item.Controllers.FirstOrDefault()?.frequency ?? "";
            RatingDisplay = "";
            VisualRange = "";
            AtisText = item.Atis?.FirstOrDefault()?.text_atis != null
                ? string.Join("\n", item.Atis.First().text_atis) : null;
            foreach (var c in item.Controllers)
                Controllers.Add(new AtcControllerRow(c.callsign, MapVatsimFacility(c.facility), c.frequency, MapVatsimRating(c.rating), FormatOnlineTime(c.logon_time)));
        }

        /// IVAO airport ATC (grouped)
        public SelectedClientViewModel(IvaoAtcItem item) when (!item.IsCtr)
        {
            Network = NetworkType.IVAO;
            ClientType = ClientType.AirportATC;
            IvaoAtcItemRef = item;
            Callsign = item.Callsign;
            AirportName = "";
            FacilityLabel = BuildIvaoFacilityLabel(item.AtcEntries);
            Frequency = item.AtcEntries?.FirstOrDefault()?.atcSession?.frequency.ToString("F3") ?? "";
            RatingDisplay = "";
            VisualRange = "";
            AtisText = null; // IVAO ATIS deferred
            if (item.AtcEntries != null)
                foreach (var e in item.AtcEntries)
                    Controllers.Add(new AtcControllerRow(e.callsign, e.atcSession?.position ?? "", e.atcSession?.frequency.ToString("F3") ?? "", "", ""));
        }

        /// IVAO CTR ATC (single entry)
        public SelectedClientViewModel(IvaoAtcItem item) when (item.IsCtr)
        {
            Network = NetworkType.IVAO;
            ClientType = ClientType.CtrATC;
            IvaoAtcItemRef = item;
            Callsign = item.Callsign;
            AirportName = "";
            FacilityLabel = "CTR";
            Frequency = item.SingleEntry?.atcSession?.frequency.ToString("F3") ?? "";
            RatingDisplay = "";
            VisualRange = "";
            AtisText = null;
            if (item.SingleEntry != null)
                Controllers.Add(new AtcControllerRow(item.Callsign, "CTR", Frequency, "", ""));
        }

        // ── Live update ──────────────────────────────────────────────────────

        public void UpdateFromVatsimPilot(VatsimAicraft item)
        {
            VatsimPilotItem = item;
            Altitude = item.Pilot.altitude;
            Groundspeed = item.Pilot.groundspeed;
            Heading = item.Pilot.heading;
            OnPropertyChanged(nameof(Altitude));
            OnPropertyChanged(nameof(Groundspeed));
            OnPropertyChanged(nameof(Heading));
            UpdateProgress();
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
            UpdateProgress();
        }

        public void AppendTrackPoint(TrackPoint pt)
        {
            _trackPoints.Add(pt);
            OnPropertyChanged(nameof(TrackPoints));
            OnPropertyChanged(nameof(TrackLocations));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void UpdateProgress()
        {
            if (string.IsNullOrEmpty(Departure) || string.IsNullOrEmpty(Arrival) || Groundspeed <= 0)
            {
                ProgressPercent = 0; EtaDisplay = ""; RemainingNmDisplay = ""; return;
            }
            // Progress requires airport coordinates — set via SetAirportCoordinates
            // Called from LiveViewViewModel after airport lookup
        }

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
            if (!_hasAirportCoords || Groundspeed <= 0) return;

            double totalNm = GeodesicUtil.DistanceNm(_depLat, _depLon, _arrLat, _arrLon);
            if (totalNm < 1) return;

            double currentLat = IvaoPilotItem?.Pilot.lastTrack.latitude ?? VatsimPilotItem?.Pilot.latitude ?? 0;
            double currentLon = IvaoPilotItem?.Pilot.lastTrack.longitude ?? VatsimPilotItem?.Pilot.longitude ?? 0;
            double flownNm = GeodesicUtil.DistanceNm(_depLat, _depLon, currentLat, currentLon);
            double remainingNm = GeodesicUtil.DistanceNm(currentLat, currentLon, _arrLat, _arrLon);

            ProgressPercent = Math.Min(100, Math.Round(flownNm / totalNm * 100, 1));
            RemainingNmDisplay = $"{(int)remainingNm} nm";

            double hoursRemaining = remainingNm / Groundspeed;
            var eta = TimeSpan.FromHours(hoursRemaining);
            EtaDisplay = eta.TotalHours >= 1
                ? $"{(int)eta.TotalHours}h {eta.Minutes:D2}m"
                : $"{eta.Minutes}m";

            OnPropertyChanged(nameof(ProgressPercent));
            OnPropertyChanged(nameof(EtaDisplay));
            OnPropertyChanged(nameof(RemainingNmDisplay));

            // Update destination line
            DestinationLine = GeodesicUtil.Interpolate(currentLat, currentLon, _arrLat, _arrLon);
        }

        // ── Static helpers ───────────────────────────────────────────────────

        private static string MapVatsimFacility(int facility) => facility switch
        {
            0 => "OBS", 1 => "FSS", 2 => "DEL", 3 => "GND",
            4 => "TWR", 5 => "APP", 6 => "CTR", _ => ""
        };

        private static string MapVatsimRating(int rating) => rating switch
        {
            1 => "OBS", 2 => "S1", 3 => "S2", 4 => "S3",
            5 => "C1", 7 => "C3", 8 => "I1", 10 => "I3",
            11 => "SUP", 12 => "ADM", _ => ""
        };

        private static string FormatOnlineTime(string? logonTime)
        {
            if (string.IsNullOrEmpty(logonTime)) return "";
            if (!DateTime.TryParse(logonTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) return "";
            var elapsed = DateTime.UtcNow - dt;
            return elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m"
                : $"{elapsed.Minutes}m";
        }

        private static string BuildFacilityLabel(IEnumerable<Controller> controllers)
        {
            var labels = controllers.Select(c => MapVatsimFacility(c.facility))
                                    .Where(l => !string.IsNullOrEmpty(l))
                                    .Distinct().ToList();
            return string.Join(" · ", labels);
        }

        private static string BuildIvaoFacilityLabel(IEnumerable<IvaoAtcEntry>? entries)
        {
            if (entries == null) return "";
            return string.Join(" · ", entries.Select(e => e.atcSession?.position ?? "").Where(p => !string.IsNullOrEmpty(p)).Distinct());
        }
    }

    public record AtcControllerRow(string Callsign, string Position, string Frequency, string Rating, string OnlineTime);
}
```

**Important:** C# does not support `when` clauses on constructors. Replace the two `IvaoAtcItem` constructors with a static factory:

```csharp
public static SelectedClientViewModel FromIvaoAtc(IvaoAtcItem item)
{
    return item.IsCtr ? new SelectedClientViewModel(item, isCtr: true)
                      : new SelectedClientViewModel(item, isCtr: false);
}
private SelectedClientViewModel(IvaoAtcItem item, bool isCtr) { /* merge both bodies */ }
```

Adjust as needed during implementation to make it compile cleanly.

- [ ] **Step 2: Build and confirm zero errors**

Build `x64|Debug`. Fix any compilation issues (namespace imports, method signatures). Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add FSTRaK/ViewModels/SelectedClientViewModel.cs
git commit -m "feat: add SelectedClientViewModel with display properties for pilot and ATC"
```

---

## Task 6: Selection state and click commands in `LiveViewViewModel`

**Files:**
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs`

- [ ] **Step 1: Add `SelectedClient` property and click commands**

In `LiveViewViewModel`, after the existing command declarations (around line 35), add:

```csharp
public RelayCommand SelectClientCommand { get; private set; }
public RelayCommand ClearSelectionCommand { get; private set; }
```

Add the backing property:

```csharp
private SelectedClientViewModel? _selectedClient;
public SelectedClientViewModel? SelectedClient
{
    get => _selectedClient;
    set { _selectedClient = value; OnPropertyChanged(); UpdateFlightPathLines(); }
}
```

Add `ObservableCollection` bindings for track polylines (declared near `FlightPath`, around line 407):

```csharp
public ObservableCollection<Location> SelectedTrackLocations { get; set; } = new();
public ObservableCollection<Location> SelectedDestinationLine { get; set; } = new();
```

- [ ] **Step 2: Initialize commands in constructor**

Find the constructor (around line 463). Add:

```csharp
SelectClientCommand = new RelayCommand(OnSelectClient);
ClearSelectionCommand = new RelayCommand(_ => SelectedClient = null);
```

- [ ] **Step 3: Implement `OnSelectClient`**

```csharp
private void OnSelectClient(object? parameter)
{
    if (parameter == null) { SelectedClient = null; return; }

    var myVatsimId = Properties.Settings.Default.VatsimId?.Trim();
    var myIvaoId   = Properties.Settings.Default.IvaoId?.Trim();
    var isInFlight = _flightManager.ActiveFlight != null;

    switch (parameter)
    {
        case VatsimAicraft va:
        {
            bool isOwn = !string.IsNullOrEmpty(myVatsimId) && va.Pilot.cid.ToString() == myVatsimId;
            var tracks = _pilotTracks.TryGetValue($"VATSIM:{va.Pilot.callsign}", out var t) ? new List<TrackPoint>(t) : new List<TrackPoint>();
            SelectedClient = new SelectedClientViewModel(va, isOwn, isOwn && isInFlight, tracks);
            TrySetAirportCoords(SelectedClient);
            break;
        }
        case IvaoAircraft ia:
        {
            bool isOwn = !string.IsNullOrEmpty(myIvaoId) && ia.Pilot.userId.ToString() == myIvaoId;
            var tracks = _pilotTracks.TryGetValue($"IVAO:{ia.Callsign}", out var t) ? new List<TrackPoint>(t) : new List<TrackPoint>();
            SelectedClient = new SelectedClientViewModel(ia, isOwn, isOwn && isInFlight, tracks);
            TrySetAirportCoords(SelectedClient);
            _ = FetchIvaoTrackAsync(ia.Pilot.userId, ia.Callsign);
            break;
        }
        case VatsimControlledAirport va:
            SelectedClient = new SelectedClientViewModel(va);
            break;
        case IvaoAtcItem iai:
            SelectedClient = SelectedClientViewModel.FromIvaoAtc(iai);
            break;
    }
}
```

- [ ] **Step 4: Implement `TrySetAirportCoords`**

FSTRaK already has an `AirportResolver` that resolves airports from a CSV. Check `Utils/AircraftResolver.cs` to confirm; if airport lookup isn't there, use a simple lat/lon from the VATSIM static data or leave coords empty (progress bar will not show). For now use the VatSIM static airport data:

```csharp
private void TrySetAirportCoords(SelectedClientViewModel client)
{
    if (string.IsNullOrEmpty(client.Departure) || string.IsNullOrEmpty(client.Arrival)) return;
    var airports = _vatsimService.VatsimStaticData?.airports;
    if (airports == null) return;
    var dep = airports.FirstOrDefault(a => a.icao == client.Departure);
    var arr = airports.FirstOrDefault(a => a.icao == client.Arrival);
    if (dep == null || arr == null) return;
    client.SetAirportCoordinates(dep.latitude, dep.longitude, arr.latitude, arr.longitude);
}
```

Check `VatsimService` for the correct property name of static airport data (it may be `VatsimStaticData` or `StaticData`). Adjust accordingly.

- [ ] **Step 5: Implement IVAO track fetch**

```csharp
private async Task FetchIvaoTrackAsync(int userId, string callsign)
{
    try
    {
        using var http = new System.Net.Http.HttpClient();
        var url = $"https://api.ivao.aero/v2/tracker/sessions/{userId}/tracks";
        var json = await http.GetStringAsync(url);
        var tracks = System.Text.Json.JsonSerializer.Deserialize<List<IvaoTrackPoint>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (tracks == null || SelectedClient?.Callsign != callsign) return;
        var converted = tracks.Select(t => new TrackPoint(t.Latitude, t.Longitude, t.Altitude, DateTime.UtcNow)).ToList();
        App.Current.Dispatcher.Invoke(() =>
        {
            SelectedClient.TrackPoints = converted;
            UpdateFlightPathLines();
        });
    }
    catch (Exception ex)
    {
        Serilog.Log.Warning(ex, "Failed to fetch IVAO track for {UserId}", userId);
    }
}

// Minimal IVAO track point deserialization model (inner class or in IvaoModel)
private class IvaoTrackPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Altitude { get; set; }
}
```

Check the actual IVAO track API response shape by calling the URL manually (replace `62007921` with a real user ID). Adjust property names to match.

- [ ] **Step 6: Implement `UpdateFlightPathLines`**

```csharp
private void UpdateFlightPathLines()
{
    SelectedTrackLocations.Clear();
    SelectedDestinationLine.Clear();
    var c = SelectedClient;
    if (c == null || !c.IsPilot) return;

    if (!c.IsOwnAircraftInFlight)
        foreach (var loc in c.TrackLocations)
            SelectedTrackLocations.Add(loc);

    if (c.DestinationLine != null)
        foreach (var loc in c.DestinationLine)
            SelectedDestinationLine.Add(loc);
}
```

Also hook `SelectedClient.PropertyChanged` to call `UpdateFlightPathLines` when `TrackPoints` or `DestinationLine` change:

In `OnSelectClient`, after setting `SelectedClient`, add:
```csharp
if (SelectedClient != null)
    SelectedClient.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName is nameof(SelectedClientViewModel.TrackPoints) or nameof(SelectedClientViewModel.DestinationLine))
            App.Current.Dispatcher.Invoke(UpdateFlightPathLines);
    };
```

- [ ] **Step 7: Update polling to refresh `SelectedClient` live data**

In `VatsimServiceOnPropertyChanged`, after calling `ProcessVatsimPilots()`, add:

```csharp
if (SelectedClient?.Network == NetworkType.VATSIM && SelectedClient.IsPilot)
{
    var match = _vatsimData?.pilots.FirstOrDefault(p => p.callsign == SelectedClient.Callsign);
    if (match != null)
    {
        var wrapper = new VatsimAicraft(match);
        SelectedClient.UpdateFromVatsimPilot(wrapper);
        if (_pilotTracks.TryGetValue($"VATSIM:{match.callsign}", out var t))
            SelectedClient.TrackPoints = new List<TrackPoint>(t);
        SelectedClient.RecalcProgress();
    }
}
```

Similarly in `IvaoServiceOnPropertyChanged`, after `ProcessIvaoPilots()`:

```csharp
if (SelectedClient?.Network == NetworkType.IVAO && SelectedClient.IsPilot)
{
    var match = _ivaoService.IvaoData?.pilots.FirstOrDefault(p => p.callsign == SelectedClient.Callsign);
    if (match != null)
    {
        var wrapper = new IvaoAircraft(match);
        SelectedClient.UpdateFromIvaoPilot(wrapper);
        SelectedClient.RecalcProgress();
    }
}
```

- [ ] **Step 8: Own aircraft suppression in `ProcessIvaoPilots` and `ProcessVatsimPilots`**

The rule: hide own network marker only when in an active MSFS flight.

In `ProcessIvaoPilots`, replace the existing exclusion:
```csharp
// existing: if (!string.IsNullOrEmpty(myId) && pilot.userId.ToString() == myId) continue;
// replace with:
bool isInFlight = _flightManager.ActiveFlight != null;
if (!string.IsNullOrEmpty(myId) && pilot.userId.ToString() == myId && isInFlight) continue;
```

In `ProcessVatsimPilots`, add a similar check. First get the VATSIM ID:
```csharp
var myVatsimId = Properties.Settings.Default.VatsimId?.Trim();
bool isInFlight = _flightManager.ActiveFlight != null;
// inside the loop, before adding:
if (!string.IsNullOrEmpty(myVatsimId) && pilot.cid.ToString() == myVatsimId && isInFlight) continue;
```

- [ ] **Step 9: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors.

- [ ] **Step 10: Commit**

```
git add FSTRaK/ViewModels/LiveViewViewModel.cs
git commit -m "feat: add selection state, click commands, track fetch, and own-aircraft suppression"
```

---

## Task 7: Wire click commands to map markers in `LiveView.xaml`

**Files:**
- Modify: `FSTRaK/Views/LiveView.xaml`

Map markers need `MouseLeftButtonUp` triggers firing `SelectClientCommand` with the item as the parameter. The cleanest WPF approach without code-behind is a `Behavior` trigger on the `MapItem` container, passing `DataContext` as the `CommandParameter`.

- [ ] **Step 1: Add click trigger to IVAO aircraft `ItemContainerStyle`**

Find the IVAO aircraft `MapItemsControl` (around line 251). The `ItemContainerStyle` targets `map:MapItem`. Add a trigger inside the style:

```xml
<Style TargetType="map:MapItem">
    <Setter Property="map:MapItem.Location" Value="{Binding Location}"/>
    <Setter Property="HorizontalAlignment" Value="Center"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="Margin" Value="-12 -12 0 0"/>
    <EventSetter Event="MouseLeftButtonUp" Handler="OnMapItemClicked"/>
</Style>
```

The `EventSetter` routes to a code-behind handler in `LiveView.xaml.cs` (added in Task 8). Pass the DataContext to the ViewModel command.

- [ ] **Step 2: Add click trigger to VATSIM aircraft `ItemContainerStyle`**

Similarly for the VATSIM aircraft `MapItemsControl` (around line 277):

```xml
<Style TargetType="map:MapItem">
    <Setter Property="map:MapItem.Location" Value="{Binding Location}"/>
    <Setter Property="HorizontalAlignment" Value="Center"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="Margin" Value="-16 -16 0 0"/>
    <EventSetter Event="MouseLeftButtonUp" Handler="OnMapItemClicked"/>
</Style>
```

- [ ] **Step 3: Add click trigger to IVAO ATC `ItemContainerStyle`**

Find the IVAO ATC `MapItemsControl` (around line 157):

```xml
<EventSetter Event="MouseLeftButtonUp" Handler="OnMapItemClicked"/>
```

- [ ] **Step 4: Add click trigger to VATSIM airport `ItemContainerStyle`**

Find the VATSIM airports `MapItemsControl` (around line 177):

```xml
<EventSetter Event="MouseLeftButtonUp" Handler="OnMapItemClicked"/>
```

- [ ] **Step 5: Add flight path `MapPolyline` bindings**

After the existing `FlightPath` / `LastSegmentLine` polylines (around lines 318-319), add:

```xml
<!-- Selected client track (solid) -->
<map:MapPolyline
    Locations="{Binding SelectedTrackLocations}"
    Stroke="#FF38bdf8"
    StrokeThickness="2"
    StrokeLineJoin="Round"
    Visibility="{Binding SelectedClient, Converter={StaticResource NullToVisConverter}}"/>

<!-- Selected client destination (dashed) -->
<map:MapPolyline
    Locations="{Binding SelectedDestinationLine}"
    Stroke="#FF38bdf8"
    StrokeThickness="1.5"
    StrokeDashArray="4 4"
    StrokeLineJoin="Round"
    Visibility="{Binding SelectedClient, Converter={StaticResource NullToVisConverter}}"/>
```

Note: `NullToVisConverter` converts null → Collapsed, non-null → Visible. Add it to `Converters.cs` (Task 9).

For IVAO accent color `#FFFF8C00` (orange), these polylines need to switch color based on network. The simplest approach: bind `Stroke` to a computed brush property on `SelectedClient`. Add `TrackStroke` and `DestStroke` properties to `SelectedClientViewModel`:

```csharp
public string TrackStrokeColor => Network == NetworkType.IVAO ? "#FFFF8C00" : "#FF38bdf8";
```

Then in XAML use a converter or inline brush. The easiest: set a fixed color per polyline and accept that both use the VATSIM blue for now, or bind via a converter. Use inline for simplicity:

```xml
<map:MapPolyline Stroke="{Binding SelectedClient.TrackStrokeColor,
    Converter={StaticResource StringToColorBrushConverter}}" .../>
```

Add `StringToColorBrushConverter` to `Converters.cs` (Task 9).

- [ ] **Step 6: Add `ClientDetailPanelControl` overlay**

Inside the `<Grid>` that wraps `<map:Map>`, after the map, add:

```xml
<views:ClientDetailPanelControl
    DataContext="{Binding SelectedClient}"
    VerticalAlignment="Bottom"
    HorizontalAlignment="Right"
    Margin="0,0,16,16"
    Visibility="{Binding SelectedClient, RelativeSource={RelativeSource AncestorType=UserControl},
                 Converter={StaticResource NullToVisConverter}}"/>
```

- [ ] **Step 7: Style the ToolTip globally for glassmorphism**

In `LiveView.xaml`'s `<UserControl.Resources>`, add a `ToolTip` style:

```xml
<Style TargetType="ToolTip">
    <Setter Property="Background" Value="#CC1a2a3a"/>
    <Setter Property="Foreground" Value="#DDFFFFFF"/>
    <Setter Property="BorderBrush" Value="#33FFFFFF"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="8,6"/>
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ToolTip">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="8"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

This automatically applies to all existing `<ToolTip Content="{Binding TooltipText}"/>` elements.

- [ ] **Step 8: Build — expect errors for missing code-behind handler and converters (fixed in Tasks 8-9)**

Build `x64|Debug`. Expected: errors for `OnMapItemClicked`, `NullToVisConverter`, `StringToColorBrushConverter`. These are intentional — fixed next.

---

## Task 8: Code-behind click handler and Escape key

**Files:**
- Modify: `FSTRaK/Views/LiveView.xaml.cs`

- [ ] **Step 1: Add `OnMapItemClicked` handler**

```csharp
private void OnMapItemClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext != null)
    {
        var vm = DataContext as LiveViewViewModel;
        vm?.SelectClientCommand.Execute(fe.DataContext);
        e.Handled = true;   // prevent map's MouseLeftButtonDown from also firing
    }
}
```

- [ ] **Step 2: Add Escape key handler**

In `OnLoaded` (around line 35), subscribe to the `KeyDown` event on the UserControl:

```csharp
KeyDown += OnKeyDown;
```

Add the handler:

```csharp
private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (e.Key == System.Windows.Input.Key.Escape)
    {
        var vm = DataContext as LiveViewViewModel;
        vm?.ClearSelectionCommand.Execute(null);
    }
}
```

In `OnUnLoaded` (around line 46), unsubscribe:

```csharp
KeyDown -= OnKeyDown;
```

- [ ] **Step 3: Build and confirm zero errors except converters**

Build `x64|Debug`. Expected: errors only for `NullToVisConverter` and `StringToColorBrushConverter` (fixed in Task 9).

- [ ] **Step 4: Commit**

```
git add FSTRaK/Views/LiveView.xaml.cs
git commit -m "feat: add map marker click handler and Escape key to clear selection"
```

---

## Task 9: Converters

**Files:**
- Modify: `FSTRaK/Utils/Converters.cs`

- [ ] **Step 1: Add `NullToVisibilityConverter`**

Open `Converters.cs`. Add:

```csharp
[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value == null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
```

Register it in `LiveView.xaml` resources:
```xml
<utils:NullToVisibilityConverter x:Key="NullToVisConverter"/>
```

- [ ] **Step 2: Add `StringToSolidColorBrushConverter`**

```csharp
[ValueConversion(typeof(string), typeof(System.Windows.Media.SolidColorBrush))]
public class StringToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string colorStr)
            try { return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr)); }
            catch { }
        return System.Windows.Media.Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
```

Register in `LiveView.xaml`:
```xml
<utils:StringToSolidColorBrushConverter x:Key="StringToColorBrushConverter"/>
```

- [ ] **Step 3: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add FSTRaK/Utils/Converters.cs FSTRaK/Views/LiveView.xaml
git commit -m "feat: add NullToVisibility and StringToSolidColorBrush converters"
```

---

## Task 10: `ClientDetailPanelControl` — XAML and code-behind

**Files:**
- Create: `FSTRaK/Views/ClientDetailPanelControl.xaml`
- Create: `FSTRaK/Views/ClientDetailPanelControl.xaml.cs`

This UserControl's `DataContext` is `SelectedClientViewModel`. It uses `DataTrigger`s to switch between the pilot panel, airport ATC panel, and CTR ATC panel.

- [ ] **Step 1: Create `ClientDetailPanelControl.xaml.cs`**

```csharp
using System.Windows.Controls;

namespace FSTRaK.Views
{
    public partial class ClientDetailPanelControl : UserControl
    {
        public ClientDetailPanelControl()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 2: Create `ClientDetailPanelControl.xaml` — shell with glassmorphism style**

```xml
<UserControl x:Class="FSTRaK.Views.ClientDetailPanelControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:FSTRaK.ViewModels"
             xmlns:utils="clr-namespace:FSTRaK.Utils"
             d:DataContext="{d:DesignInstance Type=vm:SelectedClientViewModel}"
             Width="260">

    <UserControl.Resources>
        <utils:NullToVisibilityConverter x:Key="NullToVis"/>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>

        <!-- Dark glass panel style -->
        <Style x:Key="GlassPanel" TargetType="Border">
            <Setter Property="Background" Value="#12FFFFFF"/>
            <Setter Property="BorderBrush" Value="#22FFFFFF"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="CornerRadius" Value="12"/>
            <Setter Property="Padding" Value="14"/>
        </Style>

        <!-- Stat tile style -->
        <Style x:Key="StatTile" TargetType="Border">
            <Setter Property="Background" Value="#33000000"/>
            <Setter Property="CornerRadius" Value="6"/>
            <Setter Property="Padding" Value="6"/>
        </Style>

        <Style x:Key="StatValue" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF7dd3fc"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="HorizontalAlignment" Value="Center"/>
        </Style>

        <Style x:Key="StatLabel" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#664DFFFFFF"/>
            <Setter Property="FontSize" Value="8"/>
            <Setter Property="HorizontalAlignment" Value="Center"/>
        </Style>

        <Style x:Key="SectionBg" TargetType="Border">
            <Setter Property="Background" Value="#33000000"/>
            <Setter Property="CornerRadius" Value="6"/>
            <Setter Property="Padding" Value="6,5"/>
            <Setter Property="Margin" Value="0,0,0,6"/>
        </Style>
    </UserControl.Resources>

    <!-- Outer dark background to simulate frost -->
    <Border Style="{StaticResource GlassPanel}">
        <Grid>
            <!-- Close button -->
            <Button Content="×" FontSize="14" Foreground="#88FFFFFF" Background="Transparent"
                    BorderThickness="0" HorizontalAlignment="Right" VerticalAlignment="Top"
                    Margin="0,-4,-4,0" Cursor="Hand"
                    Command="{Binding DataContext.ClearSelectionCommand,
                              RelativeSource={RelativeSource AncestorType=UserControl}}"/>

            <!-- Pilot panel -->
            <StackPanel Visibility="{Binding IsPilot, Converter={StaticResource BoolToVis}}">
                <!-- Header -->
                <Grid Margin="0,0,0,10">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <!-- Airline logo box -->
                    <Border Grid.Column="0" Width="40" Height="40" Margin="0,0,10,0"
                            Background="#1AFFFFFF" BorderBrush="#22FFFFFF" BorderThickness="1" CornerRadius="8">
                        <Grid>
                            <!-- Logo image (shown when not null) -->
                            <Image Source="{Binding AirlineLogo}" Stretch="Uniform" Margin="4"
                                   Visibility="{Binding AirlineLogo, Converter={StaticResource NullToVis}}"/>
                            <!-- Network logo fallback -->
                            <Image Source="{Binding NetworkLogo}" Stretch="Uniform" Margin="6"
                                   Visibility="{Binding AirlineLogo, Converter={StaticResource NullToVis}, ConverterParameter=invert}"/>
                        </Grid>
                    </Border>
                    <!-- Callsign + name -->
                    <StackPanel Grid.Column="1" VerticalAlignment="Center">
                        <DockPanel>
                            <TextBlock Text="{Binding Callsign}" Foreground="White" FontWeight="Bold"
                                       FontSize="15" LetterSpacing="1" DockPanel.Dock="Left"/>
                            <Border Background="#2034D399" BorderBrush="#3034D399" BorderThickness="1"
                                    CornerRadius="10" Padding="5,1" Margin="6,0,0,0"
                                    HorizontalAlignment="Left" VerticalAlignment="Center">
                                <TextBlock Text="{Binding FlightRules}" Foreground="#FF34D399"
                                           FontSize="9" FontWeight="Bold"/>
                            </Border>
                        </DockPanel>
                        <TextBlock Foreground="#66FFFFFF" FontSize="10">
                            <Run Text="{Binding PilotName, Mode=OneWay}"/>
                            <Run Text=" · "/><Run Text="{Binding CidDisplay, Mode=OneWay}"/>
                        </TextBlock>
                    </StackPanel>
                </Grid>

                <!-- Route bar -->
                <Border Style="{StaticResource SectionBg}">
                    <StackPanel>
                        <Grid Margin="0,0,0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="{Binding Departure}" Foreground="#FF7dd3fc"
                                           FontWeight="Bold" FontSize="13"/>
                            </StackPanel>
                            <!-- Progress indicator (simplified line) -->
                            <Grid Grid.Column="1" Margin="6,0">
                                <Rectangle Height="1" Fill="#33FFFFFF" VerticalAlignment="Center"/>
                                <TextBlock Text="✈" Foreground="White" FontSize="9"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="{Binding Arrival}" Foreground="#887dd3fc"
                                           FontWeight="Bold" FontSize="13"/>
                            </StackPanel>
                        </Grid>
                        <ProgressBar Value="{Binding ProgressPercent}" Maximum="100" Height="3"
                                     Background="#22FFFFFF" BorderThickness="0" Margin="0,0,0,3">
                            <ProgressBar.Foreground>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                    <GradientStop Color="#FF38bdf8" Offset="0"/>
                                    <GradientStop Color="#FF7dd3fc" Offset="1"/>
                                </LinearGradientBrush>
                            </ProgressBar.Foreground>
                        </ProgressBar>
                        <DockPanel>
                            <TextBlock Foreground="#55FFFFFF" FontSize="9" DockPanel.Dock="Left">
                                <Run Text="{Binding ProgressPercent, StringFormat={}{0:F0}%, Mode=OneWay}"/>
                                <Run Text=" · "/><Run Text="{Binding RemainingNmDisplay, Mode=OneWay}"/>
                            </TextBlock>
                            <TextBlock Text="{Binding EtaDisplay}" Foreground="#55FFFFFF"
                                       FontSize="9" HorizontalAlignment="Right" DockPanel.Dock="Right"/>
                        </DockPanel>
                    </StackPanel>
                </Border>

                <!-- Stats grid: 3 columns, 2 rows -->
                <UniformGrid Columns="3" Margin="0,0,0,6">
                    <Border Style="{StaticResource StatTile}" Margin="0,0,2,2">
                        <StackPanel>
                            <TextBlock Text="{Binding Altitude, StringFormat={}{0:N0}}" Style="{StaticResource StatValue}"/>
                            <TextBlock Text="ft ALT" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="1,0,1,2">
                        <StackPanel>
                            <TextBlock Text="{Binding Groundspeed}" Style="{StaticResource StatValue}"/>
                            <TextBlock Text="kts GS" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="2,0,0,2">
                        <StackPanel>
                            <TextBlock Text="{Binding Heading, StringFormat={}{0}°}" Style="{StaticResource StatValue}"/>
                            <TextBlock Text="HDG" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="0,2,2,0">
                        <StackPanel>
                            <TextBlock Text="{Binding AircraftType}" Foreground="#FFE2E8F0"
                                       FontSize="11" FontWeight="SemiBold" HorizontalAlignment="Center"/>
                            <TextBlock Text="ACFT" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="1,2,1,0">
                        <StackPanel>
                            <TextBlock Text="{Binding Squawk}" Foreground="#FFE2E8F0"
                                       FontSize="11" FontWeight="SemiBold" HorizontalAlignment="Center"/>
                            <TextBlock Text="SQWK" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource StatTile}" Margin="2,2,0,0">
                        <StackPanel>
                            <TextBlock Text="{Binding OnlineTime}" Foreground="#FFE2E8F0"
                                       FontSize="11" FontWeight="SemiBold" HorizontalAlignment="Center"/>
                            <TextBlock Text="ONLINE" Style="{StaticResource StatLabel}"/>
                        </StackPanel>
                    </Border>
                </UniformGrid>

                <!-- Route string -->
                <Border Style="{StaticResource SectionBg}"
                        Visibility="{Binding RouteString, Converter={StaticResource NullToVis}}">
                    <StackPanel>
                        <TextBlock Text="ROUTE" Foreground="#44FFFFFF" FontSize="8" Margin="0,0,0,2"/>
                        <TextBlock Text="{Binding RouteString}" Foreground="#99FFFFFF"
                                   FontFamily="Courier New" FontSize="9"
                                   TextTrimming="CharacterEllipsis" TextWrapping="NoWrap"/>
                    </StackPanel>
                </Border>

                <!-- Remarks -->
                <Border Style="{StaticResource SectionBg}"
                        Visibility="{Binding Remarks, Converter={StaticResource NullToVis}}">
                    <StackPanel>
                        <TextBlock Text="REMARKS" Foreground="#44FFFFFF" FontSize="8" Margin="0,0,0,2"/>
                        <TextBlock Text="{Binding Remarks}" Foreground="#77FFFFFF"
                                   FontSize="9" TextTrimming="CharacterEllipsis" TextWrapping="NoWrap"/>
                    </StackPanel>
                </Border>
            </StackPanel>

            <!-- Airport ATC panel -->
            <StackPanel Visibility="{Binding IsAirportATC, Converter={StaticResource BoolToVis}}">
                <!-- Header: network logo + ICAO + badges -->
                <StackPanel Margin="0,0,0,10">
                    <DockPanel Margin="0,0,0,2">
                        <Image Source="{Binding NetworkLogo}" Width="16" Height="16"
                               Margin="0,0,6,0" DockPanel.Dock="Left" VerticalAlignment="Center"/>
                        <TextBlock Text="{Binding Callsign}" Foreground="White"
                                   FontWeight="Bold" FontSize="15"/>
                    </DockPanel>
                    <TextBlock Text="{Binding AirportName}" Foreground="#66FFFFFF" FontSize="10"/>
                    <TextBlock Text="{Binding FacilityLabel}" Foreground="#887dd3fc"
                               FontSize="10" Margin="0,2,0,0"/>
                </StackPanel>

                <!-- Controller table -->
                <Border Style="{StaticResource SectionBg}">
                    <ItemsControl ItemsSource="{Binding Controllers}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin="0,2">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" Text="{Binding Callsign}"
                                               Foreground="#CCE2E8F0" FontSize="10" FontWeight="SemiBold"/>
                                    <TextBlock Grid.Column="1" Text="{Binding Position}"
                                               Foreground="#887dd3fc" FontSize="9" Margin="6,0"/>
                                    <TextBlock Grid.Column="2" Text="{Binding Frequency}"
                                               Foreground="#AAE2E8F0" FontSize="10" Margin="6,0,0,0"/>
                                    <TextBlock Grid.Column="3" Text="{Binding OnlineTime}"
                                               Foreground="#55FFFFFF" FontSize="9" Margin="6,0,0,0"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </Border>

                <!-- ATIS -->
                <Border Style="{StaticResource SectionBg}"
                        Visibility="{Binding AtisText, Converter={StaticResource NullToVis}}">
                    <StackPanel>
                        <TextBlock Text="ATIS" Foreground="#44FFFFFF" FontSize="8" Margin="0,0,0,2"/>
                        <TextBlock Text="{Binding AtisText}" Foreground="#88FFFFFF"
                                   FontSize="9" TextWrapping="Wrap" MaxHeight="80"/>
                    </StackPanel>
                </Border>
            </StackPanel>

            <!-- CTR / APP / FSS ATC panel -->
            <StackPanel Visibility="{Binding IsCtrATC, Converter={StaticResource BoolToVis}}">
                <DockPanel Margin="0,0,0,6">
                    <Image Source="{Binding NetworkLogo}" Width="16" Height="16"
                           Margin="0,0,6,0" DockPanel.Dock="Left" VerticalAlignment="Center"/>
                    <StackPanel>
                        <DockPanel>
                            <Border Background="#33FF8C00" BorderBrush="#44FF8C00" BorderThickness="1"
                                    CornerRadius="10" Padding="5,1" Margin="0,0,6,0">
                                <TextBlock Text="{Binding FacilityLabel}" Foreground="#FFFF8C00"
                                           FontSize="9" FontWeight="Bold"/>
                            </Border>
                            <TextBlock Text="{Binding Callsign}" Foreground="White"
                                       FontWeight="Bold" FontSize="14"/>
                        </DockPanel>
                        <TextBlock Foreground="#55FFFFFF" FontSize="10">
                            <Run Text="{Binding PilotName, Mode=OneWay}"/>
                            <Run Text=" · "/><Run Text="{Binding CidDisplay, Mode=OneWay}"/>
                        </TextBlock>
                    </StackPanel>
                </DockPanel>
                <!-- Frequency prominent -->
                <Border Style="{StaticResource SectionBg}" Margin="0,0,0,6">
                    <DockPanel>
                        <StackPanel DockPanel.Dock="Left" Margin="0,0,16,0">
                            <TextBlock Text="{Binding Frequency}" Foreground="#FF7dd3fc"
                                       FontSize="18" FontWeight="Bold"/>
                            <TextBlock Text="MHz" Foreground="#44FFFFFF" FontSize="9"/>
                        </StackPanel>
                        <UniformGrid Columns="2">
                            <StackPanel Margin="0,0,6,0">
                                <TextBlock Text="{Binding RatingDisplay}" Foreground="#CCE2E8F0"
                                           FontSize="11" FontWeight="SemiBold"/>
                                <TextBlock Text="RATING" Foreground="#44FFFFFF" FontSize="8"/>
                            </StackPanel>
                            <StackPanel>
                                <TextBlock Text="{Binding OnlineTime}" Foreground="#CCE2E8F0"
                                           FontSize="11" FontWeight="SemiBold"/>
                                <TextBlock Text="ONLINE" Foreground="#44FFFFFF" FontSize="8"/>
                            </StackPanel>
                        </UniformGrid>
                    </DockPanel>
                </Border>
                <!-- ATIS -->
                <Border Style="{StaticResource SectionBg}"
                        Visibility="{Binding AtisText, Converter={StaticResource NullToVis}}">
                    <StackPanel>
                        <TextBlock Text="ATIS" Foreground="#44FFFFFF" FontSize="8" Margin="0,0,0,2"/>
                        <TextBlock Text="{Binding AtisText}" Foreground="#88FFFFFF"
                                   FontSize="9" TextWrapping="Wrap" MaxHeight="80"/>
                    </StackPanel>
                </Border>
            </StackPanel>
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 3: Fix `NullToVis` invert for airline logo fallback**

The `NullToVisibilityConverter` doesn't support invert. Add an `InvertedNullToVisibilityConverter`:

```csharp
[ValueConversion(typeof(object), typeof(Visibility))]
public class InvertedNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value == null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
```

Register as `<utils:InvertedNullToVisibilityConverter x:Key="InvertedNullToVis"/>` in `ClientDetailPanelControl.xaml` resources and update the network logo fallback `Visibility` binding.

- [ ] **Step 4: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add FSTRaK/Views/ClientDetailPanelControl.xaml FSTRaK/Views/ClientDetailPanelControl.xaml.cs FSTRaK/Utils/Converters.cs
git commit -m "feat: add ClientDetailPanelControl glassmorphism UI"
```

---

## Task 11: Register `ClientDetailPanelControl` in `LiveView.xaml` and wire `ClearSelectionCommand`

**Files:**
- Modify: `FSTRaK/Views/LiveView.xaml`

- [ ] **Step 1: Verify namespace already declared**

At the top of `LiveView.xaml`, confirm `xmlns:views="clr-namespace:FSTRaK.Views"` is present (it is — line 9 per the exploration). No change needed.

- [ ] **Step 2: Verify panel overlay added in Task 7 Step 6**

The `<views:ClientDetailPanelControl .../>` was added in Task 7 Step 6. Confirm it is present in the file.

- [ ] **Step 3: Wire `ClearSelectionCommand` to map background click**

The map already fires `StopCenterOnAirplaneCommand` on `MouseLeftButtonDown`. We need to also clear selection when clicking empty map space (not a marker — that's handled by `e.Handled = true` in the marker click handler).

In `LiveView.xaml`, find the existing trigger on the map:

```xml
<b:EventTrigger EventName="MouseLeftButtonDown">
    <b:InvokeCommandAction Command="{Binding StopCenterOnAirplaneCommand}"/>
</b:EventTrigger>
```

Add a second action:

```xml
<b:EventTrigger EventName="MouseLeftButtonDown">
    <b:InvokeCommandAction Command="{Binding StopCenterOnAirplaneCommand}"/>
    <b:InvokeCommandAction Command="{Binding ClearSelectionCommand}"/>
</b:EventTrigger>
```

Since marker clicks set `e.Handled = true`, the map's `MouseLeftButtonDown` only fires when no marker is hit — so `ClearSelectionCommand` fires only on empty-space clicks. ✓

- [ ] **Step 4: Build and confirm zero errors**

Build `x64|Debug`. Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add FSTRaK/Views/LiveView.xaml
git commit -m "feat: wire ClearSelectionCommand to map background click"
```

---

## Task 12: Manual smoke test

- [ ] **Step 1: Run the application in debug mode**

Launch `FSTRaK` in `Debug|x64`. Open MSFS or let the network feeds run. Verify:

1. Hovering over a pilot or ATC marker shows the styled glassmorphism tooltip (dark background, rounded corners, correct data).
2. Clicking an IVAO pilot marker opens the panel in the bottom-right corner with callsign, route bar, and stat grid.
3. Clicking a VATSIM pilot marker opens the panel with the same layout.
4. The solid track polyline appears on the map for the selected pilot (may be short if session is new).
5. The dashed geodesic line extends from the aircraft to its destination airport.
6. For an IVAO pilot, the full track loads from the API within a few seconds (network call).
7. Clicking an airport ATC marker shows the merged controller table.
8. Clicking a CTR/FSS marker shows the CTR panel with frequency.
9. Pressing Escape clears the panel.
10. Clicking empty map space clears the panel.
11. Clicking another marker while one is selected switches to the new one.
12. If user's IVAO/VATSIM ID is configured and they are not in MSFS, their own network marker is visible and clickable.

- [ ] **Step 2: Commit final notes or minor fixes found during smoke test**

```
git add -A
git commit -m "fix: smoke test corrections for client detail panel"
```

---

## Self-Review

### Spec coverage check

| Spec requirement | Task |
|---|---|
| Glassmorphism popup on click, bottom-right | Task 7, 10 |
| Stays open until dismissed | Task 6, 8, 11 |
| Close button, Escape, empty-space click | Task 8, 10, 11 |
| Pilot panel: airline logo, network logo, callsign, name, CID | Task 4, 5, 10 |
| Pilot panel: route bar, progress, ETA, remaining nm | Task 5, 10 |
| Pilot panel: stats grid (alt, GS, hdg, acft, squawk, online) | Task 5, 10 |
| Pilot panel: route string, remarks | Task 5, 10 |
| Airport ATC panel: merged controller table | Task 5, 10 |
| CTR/FSS ATC panel: callsign, frequency, rating | Task 5, 10 |
| VATSIM ATIS from ATIS controller | Task 5, 10 |
| IVAO ATIS: deferred placeholder | Task 5, 10 |
| Hover tooltip: glassmorphism styled | Task 7 |
| Hover tooltip: pilot compact data | Task 7 (existing TooltipText used, styled) |
| Solid track polyline | Task 6, 7 |
| Dashed geodesic destination line | Task 3, 5, 6, 7 |
| Track accumulation (poll-cycle) | Task 1 |
| IVAO track API fetch on selection | Task 6 |
| VATSIM track: local accumulation only (StatSim deferred) | Task 1, 6 |
| Own aircraft: show when not in flight, hide when in flight | Task 6 |
| Own aircraft in flight: SimConnect aircraft clickable | Task 6 (IsOwnAircraftInFlight flag, no solid trail) |
| Own aircraft: show dashed destination line | Task 5, 6 |
| Network accent colors (VATSIM blue, IVAO orange) | Task 5, 7 |
| Airline logos from Assets/AirlineLogos/ | Task 4 |
| Network logos | Task 4 |

All spec requirements covered. ✓

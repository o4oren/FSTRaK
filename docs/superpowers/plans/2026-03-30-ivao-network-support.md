# IVAO Network Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add IVAO live network support to FSTRaK's live map, letting users switch between VATSIM and IVAO to view pilots and ATC, with a unified Pilots/ATC toggle UI replacing the three existing VATSIM-specific toggles.

**Architecture:** Mirror the existing `VatsimService` singleton pattern with a new `IvaoService` that polls two IVAO endpoints every 60 seconds. `LiveViewViewModel` gains a `NetworkType` enum property that drives which service runs and which map layers are visible. The UI toggle panel is replaced with a network selector (VATSIM / IVAO) plus two toggles (Pilots / ATC).

**Tech Stack:** C# / .NET Framework 4.7.2, WPF, XAML, Newtonsoft.Json, MapControl.WPF, Serilog. No new dependencies required.

---

## File Map

**New files:**
- `FSTRaK/DataTypes/NetworkType.cs` — enum `{ None, Vatsim, Ivao }`
- `FSTRaK/BusinessLogic/IvaoService/IvaoService.cs` — singleton, polls IVAO, exposes `IvaoData`
- `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoData.cs` — container for pilots + ATC lists
- `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoPilot.cs` — pilot JSON model
- `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoAtcSession.cs` — ATC JSON model
- `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoAtcPosition.cs` — ATC sub-object (position/session info)
- `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoLastTrack.cs` — lastTrack sub-object (flight plan / polygon)

**Modified files:**
- `FSTRaK/Properties/Settings.settings` — add `IvaoId` string setting
- `FSTRaK/ViewModels/SettingsViewModel.cs` — add `IvaoId` property
- `FSTRaK/Views/SettingsView.xaml` — add IVAO ID text field
- `FSTRaK/ViewModels/LiveViewViewModel.cs` — add `ActiveNetwork`, `IsShowPilots`, `IsShowAtc`, IVAO collections, commands, handler, processing methods
- `FSTRaK/Views/LiveView.xaml` — replace 3 VATSIM toggles with network selector + Pilots/ATC toggles; add IVAO map layers
- `README.md` — update Features and Roadmap sections

---

## Task 1: ~~Inspect IVAO API JSON structure~~ (already done — see below)

API structure confirmed. Both endpoints return a flat JSON array (no wrapper object).

**Pilots summary** — each entry:
```json
{
  "id": 61992465,
  "userId": 574753,
  "callsign": "N720PM",
  "connectionType": "PILOT",
  "isMilitary": false,
  "lastTrack": {
    "altitude": 33869,
    "groundSpeed": 256,
    "heading": 242,
    "latitude": 7.308713,
    "longitude": 53.602158,
    "onGround": false,
    "state": "En Route"
  },
  "flightPlan": {
    "aircraftId": "BE60",
    "departureId": "RJNT",
    "arrivalId": "SKCL"
  }
}
```

**ATC summary** — each entry:
```json
{
  "id": 62003413,
  "userId": 734259,
  "callsign": "SBWH_APP",
  "connectionType": "ATC",
  "atcSession": {
    "frequency": 119.65,
    "position": "APP"
  },
  "atcPosition": {
    "airportId": "SBWH",
    "atcCallsign": "Belo Horizonte Control",
    "position": "APP",
    "regionMap": [ { "lat": -19.56, "lng": -44.77 }, ... ],
    "airport": {
      "icao": "SBWH",
      "latitude": -19.85,
      "longitude": -43.95
    }
  },
  "subcenter": null
}
```

Notes: `frequency` is a `double`. No `textAtis` in the summary endpoint. ATC location comes from `atcPosition.airport.latitude/longitude`. Polygon is in `atcPosition.regionMap` as `{lat, lng}` objects. `subcenter` entries (CTR positions) have their own `latitude`/`longitude` and `regionMap` directly on the subcenter object.

Skip to Task 2.

---

## Task 2: Add `NetworkType` enum and IVAO model classes

**Files:**
- Create: `FSTRaK/DataTypes/NetworkType.cs`
- Create: `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoData.cs`
- Create: `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoPilot.cs`
- Create: `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoAtcSession.cs`
- Create: `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoAtcPosition.cs`
- Create: `FSTRaK/BusinessLogic/IvaoService/IvaoModel/IvaoLastTrack.cs`

- [ ] **Step 1: Create `NetworkType.cs`**

Pattern reference: `FSTRaK/DataTypes/FlightOutcome.cs` (same file structure).

```csharp
namespace FSTRaK.DataTypes
{
    public enum NetworkType
    {
        None,
        Vatsim,
        Ivao
    }
}
```

- [ ] **Step 2: Create `IvaoPilot.cs`**

```csharp
namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoPilot
    {
        public int userId { get; set; }
        public string callsign { get; set; }
        public IvaoLastTrack lastTrack { get; set; }
        public IvaoFlightPlan flightPlan { get; set; }
    }
}
```

- [ ] **Step 3: Create `IvaoLastTrack.cs`**

Only used for pilots (`lastTrack` does not appear in ATC entries).

```csharp
namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoLastTrack
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
        public int altitude { get; set; }
        public int heading { get; set; }
        public int groundSpeed { get; set; }   // camelCase — matches JSON
        public bool onGround { get; set; }
        public string state { get; set; }
    }
}
```

- [ ] **Step 3b: Create `IvaoFlightPlan.cs`**

```csharp
namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoFlightPlan
    {
        public string aircraftId { get; set; }
        public string departureId { get; set; }
        public string arrivalId { get; set; }
    }
}
```

- [ ] **Step 4: Create `IvaoAtcEntry.cs`** (top-level ATC object)

Note: the JSON field `atcSession` conflicts with the class name pattern; use `IvaoAtcEntry` for the top-level object and `IvaoAtcSessionInfo` for the nested `atcSession` sub-object.

```csharp
namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoAtcEntry
    {
        public int userId { get; set; }
        public string callsign { get; set; }
        public IvaoAtcSessionInfo atcSession { get; set; }
        public IvaoAtcPositionInfo atcPosition { get; set; }
        public IvaoSubcenter subcenter { get; set; }
    }
}
```

- [ ] **Step 5: Create `IvaoAtcSessionInfo.cs`**

```csharp
namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoAtcSessionInfo
    {
        public double frequency { get; set; }
        public string position { get; set; }   // e.g. "APP", "CTR", "TWR"
    }
}
```

- [ ] **Step 5b: Create `IvaoAtcPositionInfo.cs`**

```csharp
using System.Collections.Generic;

namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoAtcPositionInfo
    {
        public string airportId { get; set; }
        public string atcCallsign { get; set; }
        public string position { get; set; }
        public List<IvaoLatLng> regionMap { get; set; }
        public IvaoAirport airport { get; set; }
    }

    public class IvaoLatLng
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class IvaoAirport
    {
        public string icao { get; set; }
        public string name { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
    }
}
```

- [ ] **Step 5c: Create `IvaoSubcenter.cs`**

CTR positions use the `subcenter` field instead of `atcPosition`. It carries its own location and polygon.

```csharp
using System.Collections.Generic;

namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoSubcenter
    {
        public string centerId { get; set; }
        public string atcCallsign { get; set; }
        public string position { get; set; }
        public double frequency { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public List<IvaoLatLng> regionMap { get; set; }
    }
}
```

- [ ] **Step 6: Create `IvaoData.cs`**

Both API endpoints return a flat JSON array. `IvaoData` is assembled in `IvaoService` — it is not a deserialization target itself.

```csharp
using System.Collections.Generic;

namespace FSTRaK.BusinessLogic.IvaoService.IvaoModel
{
    public class IvaoData
    {
        public List<IvaoPilot> pilots { get; set; }
        public List<IvaoAtcEntry> atcEntries { get; set; }
    }
}
```

- [ ] **Update the File Map at the top of this plan** — replace `IvaoAtcSession.cs` and `IvaoAtcPosition.cs` with the actual file names above:
  - `IvaoAtcEntry.cs` — top-level ATC JSON object
  - `IvaoAtcSessionInfo.cs` — nested `atcSession` sub-object
  - `IvaoAtcPositionInfo.cs` — nested `atcPosition` sub-object (includes `IvaoLatLng`, `IvaoAirport`)
  - `IvaoSubcenter.cs` — subcenter (CTR) sub-object
  - `IvaoFlightPlan.cs` — pilot flight plan sub-object

- [ ] **Step 7: Add all new files to the project**

In Visual Studio, right-click each new folder/file in Solution Explorer → "Include in Project". Verify the files appear under the correct namespace in Solution Explorer. Build the solution (`Build → Build Solution`) to confirm no compile errors before continuing.

- [ ] **Step 8: Commit**

```bash
git add FSTRaK/DataTypes/NetworkType.cs
git add FSTRaK/BusinessLogic/IvaoService/
git commit -m "feat: add NetworkType enum and IVAO model classes"
```

---

## Task 3: Create `IvaoService`

**Files:**
- Create: `FSTRaK/BusinessLogic/IvaoService/IvaoService.cs`

Reference file: `FSTRaK/BusinessLogic/VatsimService/VatsimService.cs` — mirror the singleton, timer, Start/Stop/GetData pattern.

- [ ] **Step 1: Create `IvaoService.cs`**

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.Models;
using Newtonsoft.Json;
using Serilog;

namespace FSTRaK.BusinessLogic.IvaoService
{
    internal class IvaoService : BaseModel
    {
        private const string PilotsUrl = "https://api.ivao.aero/v2/tracker/now/pilots/summary";
        private const string AtcUrl = "https://api.ivao.aero/v2/tracker/now/atc/summary";
        private const int ConnectionInterval = 60 * 1000;

        private System.Timers.Timer _connectionTimer;

        public bool Started { get; private set; }

        private IvaoData _ivaoData;
        public IvaoData IvaoData
        {
            get => _ivaoData;
            private set
            {
                if (value != _ivaoData)
                {
                    _ivaoData = value;
                    OnPropertyChanged();
                }
            }
        }

        private static readonly object Lock = new();
        private static IvaoService _instance;
        public static IvaoService Instance
        {
            get
            {
                lock (Lock)
                {
                    return _instance ??= new IvaoService();
                }
            }
        }

        private IvaoService()
        {
            _connectionTimer = new System.Timers.Timer(ConnectionInterval);
            _connectionTimer.Elapsed += async (sender, e) => await GetIvaoData();
            _connectionTimer.AutoReset = true;
        }

        public async void Start()
        {
            Log.Information("Starting to poll IVAO for data");
            await GetIvaoData();
            _connectionTimer.Start();
            Started = true;
        }

        public void Stop()
        {
            Log.Information("Stopping IVAO polling");
            IvaoData = null;
            _connectionTimer.Stop();
            Started = false;
        }

        private async Task GetIvaoData()
        {
            try
            {
                Log.Debug("Fetching IVAO data");
                using var client = new HttpClient();

                var pilotsTask = client.GetStringAsync(PilotsUrl);
                var atcTask = client.GetStringAsync(AtcUrl);
                await Task.WhenAll(pilotsTask, atcTask);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var data = new IvaoData
                    {
                        pilots = JsonConvert.DeserializeObject<System.Collections.Generic.List<IvaoPilot>>(pilotsTask.Result),
                        atcEntries = JsonConvert.DeserializeObject<System.Collections.Generic.List<IvaoAtcEntry>>(atcTask.Result)
                    };
                    IvaoData = data;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while calling IVAO");
            }
        }
    }
}
```

- [ ] **Step 2: Add to project in Visual Studio**

Right-click `IvaoService.cs` in Solution Explorer → "Include in Project". Build the solution to confirm no compile errors.

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/BusinessLogic/IvaoService/IvaoService.cs
git commit -m "feat: add IvaoService singleton with polling"
```

---

## Task 4: Add IVAO ID to Settings

**Files:**
- Modify: `FSTRaK/Properties/Settings.settings`
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`
- Modify: `FSTRaK/Views/SettingsView.xaml`

- [ ] **Step 1: Add `IvaoId` to `Settings.settings`**

Open `FSTRaK/Properties/Settings.settings` in Visual Studio's settings editor (double-click the file). Add a new row:
- Name: `IvaoId`
- Type: `string`
- Scope: `User`
- Value: *(leave empty)*

Alternatively, add directly to the XML. Open the file and add this entry inside `<Settings>`:

```xml
<Setting Name="IvaoId" Type="System.String" Scope="User">
    <Value Profile="(Default)" />
</Setting>
```

- [ ] **Step 2: Add `IvaoId` property to `SettingsViewModel.cs`**

Open `FSTRaK/ViewModels/SettingsViewModel.cs`. Find the `VatsimId` property (around line 287) and add the `IvaoId` property immediately after it:

```csharp
private string _ivaoId;
public string IvaoId
{
    get => _ivaoId;
    set
    {
        _ivaoId = value;
        Properties.Settings.Default.IvaoId = _ivaoId;
        OnPropertyChanged();
    }
}
```

Also find where `VatsimId` is initialized in the constructor (search for `VatsimId =` or `_vatsimId =`) and add the IVAO equivalent immediately after:

```csharp
_ivaoId = Properties.Settings.Default.IvaoId;
```

- [ ] **Step 3: Add IVAO ID field to `SettingsView.xaml`**

Open `FSTRaK/Views/SettingsView.xaml`. Find the VATSIM ID `StackPanel` (search for `VATSIM ID`). Add an identical block immediately after it:

```xaml
<StackPanel Orientation="Horizontal" Margin="10" ToolTipService.ShowDuration="5000">
    <Label Style="{DynamicResource FSTrAkLabel}" Width="250">IVAO ID</Label>
    <TextBox FontFamily="{DynamicResource CurrentFont}"
             Foreground="{DynamicResource TextColor}"
             Background="{DynamicResource ControlBackgroundColorBrush}"
             FontSize="{DynamicResource ControlFontSize}"
             Width="200"
             Text="{Binding IvaoId}" Cursor="Arrow" TextAlignment="Center" Padding="0 8 0 0"/>
    <StackPanel.ToolTip>
        Type your IVAO ID to prevent duplicate representation of your aircraft when IVAO aircraft are shown on the map
    </StackPanel.ToolTip>
</StackPanel>
```

- [ ] **Step 4: Build and verify**

Build the solution (`Build → Build Solution`). Expected: no errors. The IVAO ID field will now appear in Settings — verify it is visible below the VATSIM ID field when you run the app.

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/Properties/Settings.settings
git add FSTRaK/ViewModels/SettingsViewModel.cs
git add FSTRaK/Views/SettingsView.xaml
git commit -m "feat: add IVAO ID setting"
```

---

## Task 5: Add IVAO support to `LiveViewViewModel`

This is the largest task. Work through it section by section.

**Files:**
- Modify: `FSTRaK/ViewModels/LiveViewViewModel.cs`

- [ ] **Step 1: Add `using` and field declarations**

At the top of `LiveViewViewModel.cs`, add the new using statement alongside the existing ones:

```csharp
using FSTRaK.BusinessLogic.IvaoService;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.DataTypes;
```

Add the `IvaoService` field alongside `_vatsimService` (around line 23):

```csharp
private readonly IvaoService _ivaoService = IvaoService.Instance;
```

- [ ] **Step 2: Add `ActiveNetwork` property**

Add after `IsShowVatsimFirs` (around line 105):

```csharp
private NetworkType _activeNetwork = NetworkType.None;
public NetworkType ActiveNetwork
{
    get => _activeNetwork;
    set
    {
        if (value != _activeNetwork)
        {
            _activeNetwork = value;
            OnPropertyChanged();
        }
    }
}
```

- [ ] **Step 3: Add `IsShowPilots` and `IsShowAtc` properties**

Add after `ActiveNetwork`:

```csharp
private bool _isShowPilots;
public bool IsShowPilots
{
    get => _isShowPilots;
    set
    {
        if (value != _isShowPilots)
        {
            _isShowPilots = value;
            // Drive VATSIM backing properties when VATSIM is active
            if (_activeNetwork == NetworkType.Vatsim)
            {
                IsShowVatsimAircraft = value;
            }
            OnPropertyChanged();
        }
    }
}

private bool _isShowAtc;
public bool IsShowAtc
{
    get => _isShowAtc;
    set
    {
        if (value != _isShowAtc)
        {
            _isShowAtc = value;
            // Drive VATSIM backing properties when VATSIM is active
            if (_activeNetwork == NetworkType.Vatsim)
            {
                IsShowVatsimAirports = value;
                IsShowVatsimFirs = value;
            }
            OnPropertyChanged();
        }
    }
}
```

- [ ] **Step 4: Add IVAO collections**

Add after the existing VATSIM collections (around line 287):

```csharp
private BindingList<IvaoAircraft> _ivaoAircraftList = new();
public BindingList<IvaoAircraft> IvaoAircraftList
{
    get => _ivaoAircraftList;
    private set
    {
        if (value != _ivaoAircraftList)
        {
            _ivaoAircraftList = value;
            OnPropertyChanged();
        }
    }
}

private BindingList<IvaoAtcItem> _ivaoAtcList = new();
public BindingList<IvaoAtcItem> IvaoAtcList
{
    get => _ivaoAtcList;
    private set
    {
        if (value != _ivaoAtcList)
        {
            _ivaoAtcList = value;
            OnPropertyChanged();
        }
    }
}
```

- [ ] **Step 5: Add nested `IvaoAircraft` and `IvaoAtcItem` classes**

Add these nested classes at the bottom of `LiveViewViewModel.cs`, alongside the existing `VatsimAicraft`, `VatsimControlledAirport`, etc. nested classes:

```csharp
internal class IvaoAircraft
{
    public Location Location { get; set; }
    public double Heading { get; set; }
    public string Icon { get; set; }
    public string Callsign { get; set; }
    public string Departure { get; set; }
    public string Destination { get; set; }
    public string Aircraft { get; set; }
    public int Altitude { get; set; }
    public int Groundspeed { get; set; }

    public IvaoAircraft(IvaoPilot pilot)
    {
        Location = new Location(pilot.lastTrack.latitude, pilot.lastTrack.longitude);
        Heading = pilot.lastTrack.heading;
        Callsign = pilot.callsign;
        Altitude = pilot.lastTrack.altitude;
        Groundspeed = pilot.lastTrack.groundSpeed;
        Departure = pilot.flightPlan?.departureId;
        Destination = pilot.flightPlan?.arrivalId;
        Aircraft = pilot.flightPlan?.aircraftId;
        Icon = AircraftResolver.GetAircraftIcon(pilot.flightPlan?.aircraftId ?? "");
    }
}

internal class IvaoAtcItem
{
    public Location Location { get; set; }
    public string Callsign { get; set; }
    public string Frequency { get; set; }
    public string DisplayName { get; set; }
    public LocationCollection ControlPolygon { get; set; }

    public IvaoAtcItem(IvaoAtcEntry entry)
    {
        Callsign = entry.callsign;
        Frequency = entry.atcSession?.frequency.ToString("F3");

        // Location and polygon: prefer atcPosition.airport, fall back to subcenter
        if (entry.atcPosition?.airport != null)
        {
            Location = new Location(entry.atcPosition.airport.latitude, entry.atcPosition.airport.longitude);
            DisplayName = entry.atcPosition.atcCallsign ?? entry.callsign;
            if (entry.atcPosition.regionMap?.Count > 0)
            {
                ControlPolygon = new LocationCollection();
                foreach (var pt in entry.atcPosition.regionMap)
                    ControlPolygon.Add(new Location(pt.lat, pt.lng));
            }
        }
        else if (entry.subcenter != null)
        {
            Location = new Location(entry.subcenter.latitude, entry.subcenter.longitude);
            DisplayName = entry.subcenter.atcCallsign ?? entry.callsign;
            if (entry.subcenter.regionMap?.Count > 0)
            {
                ControlPolygon = new LocationCollection();
                foreach (var pt in entry.subcenter.regionMap)
                    ControlPolygon.Add(new Location(pt.lat, pt.lng));
            }
        }
    }
}

- [ ] **Step 6: Add `SelectNetworkCommand` and replace Enable/Disable commands**

In the constructor (around line 351), **replace** the existing `EnableVatsimItemCommand` and `DisableVatsimItemCommand` initializations with:

```csharp
SelectNetworkCommand = new RelayCommand(o =>
{
    var network = (NetworkType)o;

    if (network == ActiveNetwork)
    {
        // Deselect: stop service, clear everything
        StopActiveNetwork();
        ActiveNetwork = NetworkType.None;
        IsShowPilots = false;
        IsShowAtc = false;
    }
    else
    {
        // Switch network
        StopActiveNetwork();
        ClearAllNetworkCollections();
        ActiveNetwork = network;
        IsShowPilots = false;
        IsShowAtc = false;
        StartActiveNetwork();
    }
});

EnableNetworkItemCommand = new RelayCommand(o =>
{
    if (ActiveNetwork == NetworkType.None) return;
    if (!_isShowPilots) ClearIvaoAircraft();
    if (!_isShowAtc) ClearIvaoAtc();
    if (!IsShowVatsimAircraft) VatsimAircraftList.Clear();
    if (!IsShowVatsimAirports) VatsimControlledAirports.Clear();
    if (!IsShowVatsimFirs) { VatsimControlledFirs.Clear(); VatsimControlledUirs.Clear(); }
    if (IsShowPilots || IsShowAtc)
        StartActiveNetwork();
});

DisableNetworkItemCommand = new RelayCommand(o =>
{
    if (ActiveNetwork == NetworkType.None) return;
    if (!IsShowPilots && !IsShowAtc)
        StopActiveNetwork();
});
```

Add command declarations at the top of the class alongside the existing ones:

```csharp
public RelayCommand SelectNetworkCommand { get; private set; }
public RelayCommand EnableNetworkItemCommand { get; private set; }
public RelayCommand DisableNetworkItemCommand { get; private set; }
```

- [ ] **Step 7: Add helper methods**

Add these private methods to the class:

```csharp
private void StartActiveNetwork()
{
    switch (ActiveNetwork)
    {
        case NetworkType.Vatsim:
            _vatsimService.Start();
            break;
        case NetworkType.Ivao:
            _ivaoService.Start();
            break;
    }
}

private void StopActiveNetwork()
{
    switch (ActiveNetwork)
    {
        case NetworkType.Vatsim:
            _vatsimService.Stop();
            break;
        case NetworkType.Ivao:
            _ivaoService.Stop();
            break;
    }
}

private void ClearAllNetworkCollections()
{
    VatsimAircraftList.Clear();
    VatsimControlledAirports.Clear();
    VatsimControlledFirs.Clear();
    VatsimControlledUirs.Clear();
    ClearIvaoAircraft();
    ClearIvaoAtc();
}

private void ClearIvaoAircraft() => IvaoAircraftList.Clear();
private void ClearIvaoAtc() => IvaoAtcList.Clear();
```

- [ ] **Step 8: Subscribe to `IvaoService` and add IVAO data handler**

In the constructor, add after `_vatsimService.PropertyChanged += VatsimServiceOnPropertyChanged;`:

```csharp
_ivaoService.PropertyChanged += IvaoServiceOnPropertyChanged;
```

Add the handler method:

```csharp
private void IvaoServiceOnPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    switch (e.PropertyName)
    {
        case nameof(IvaoService.IvaoData):
            if (IsShowPilots)
                ProcessIvaoPilots();
            else
                IvaoAircraftList.Clear();

            if (IsShowAtc)
                ProcessIvaoAtc();
            else
                IvaoAtcList.Clear();
            break;
    }
}
```

- [ ] **Step 9: Add `ProcessIvaoPilots` and `ProcessIvaoAtc`**

```csharp
private async void ProcessIvaoPilots()
{
    if (_ivaoService.IvaoData?.pilots == null) return;
    var newList = new System.Collections.Generic.List<IvaoAircraft>();
    var myId = Properties.Settings.Default.IvaoId;
    await Task.Run(() =>
    {
        foreach (var pilot in _ivaoService.IvaoData.pilots)
        {
            if (!string.IsNullOrEmpty(myId) && pilot.userId.ToString() == myId) continue;
            if (pilot.lastTrack == null) continue;
            newList.Add(new IvaoAircraft(pilot));
        }
    });
    IvaoAircraftList.ReplaceContent(newList);
}

private async void ProcessIvaoAtc()
{
    if (_ivaoService.IvaoData?.atcEntries == null) return;
    var newList = new System.Collections.Generic.List<IvaoAtcItem>();
    await Task.Run(() =>
    {
        foreach (var atc in _ivaoService.IvaoData.atcEntries)
        {
            // Skip entries with no location data
            if (atc.atcPosition?.airport == null && atc.subcenter == null) continue;
            newList.Add(new IvaoAtcItem(atc));
        }
    });
    IvaoAtcList.ReplaceContent(newList);
}
```

- [ ] **Step 10: Build the solution**

`Build → Build Solution`. Fix any compile errors. Expected: clean build with no errors.

- [ ] **Step 11: Commit**

```bash
git add FSTRaK/ViewModels/LiveViewViewModel.cs
git commit -m "feat: add IVAO support to LiveViewViewModel"
```

---

## Task 6: Update `LiveView.xaml` — toggle panel and map layers

**Files:**
- Modify: `FSTRaK/Views/LiveView.xaml`

- [ ] **Step 1: Replace the three VATSIM toggle buttons with the new panel**

Find the `<StackPanel Margin="10" HorizontalAlignment="Right">` that contains the toggle buttons (around line 254). Replace the three VATSIM `ToggleButton`s (Pilots, Airports, FIRs — keep the center-on-airplane button) with:

```xaml
<!-- Network selector -->
<StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,4,0,0">
    <ToggleButton Style="{DynamicResource MapToggleButton}"
                  IsChecked="{Binding ActiveNetwork, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Vatsim}">
        <b:Interaction.Triggers>
            <b:EventTrigger EventName="Click">
                <b:InvokeCommandAction Command="{Binding SelectNetworkCommand}"
                                       CommandParameter="{x:Static dataTypes:NetworkType.Vatsim}"/>
            </b:EventTrigger>
        </b:Interaction.Triggers>
        <TextBlock Foreground="{DynamicResource SuperBrightTextColor}" FontSize="11" FontWeight="Bold">VATSIM</TextBlock>
    </ToggleButton>
    <ToggleButton Style="{DynamicResource MapToggleButton}"
                  IsChecked="{Binding ActiveNetwork, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Ivao}">
        <b:Interaction.Triggers>
            <b:EventTrigger EventName="Click">
                <b:InvokeCommandAction Command="{Binding SelectNetworkCommand}"
                                       CommandParameter="{x:Static dataTypes:NetworkType.Ivao}"/>
            </b:EventTrigger>
        </b:Interaction.Triggers>
        <TextBlock Foreground="{DynamicResource SuperBrightTextColor}" FontSize="11" FontWeight="Bold">IVAO</TextBlock>
    </ToggleButton>
</StackPanel>

<!-- Pilots toggle — visible only when a network is selected -->
<ToggleButton Style="{DynamicResource MapToggleButton}"
              IsChecked="{Binding IsShowPilots}"
              Visibility="{Binding ActiveNetwork, Converter={StaticResource NetworkTypeToVisConverter}}">
    <b:Interaction.Triggers>
        <b:EventTrigger EventName="Checked">
            <b:InvokeCommandAction Command="{Binding EnableNetworkItemCommand}"/>
        </b:EventTrigger>
        <b:EventTrigger EventName="Unchecked">
            <b:InvokeCommandAction Command="{Binding DisableNetworkItemCommand}"/>
        </b:EventTrigger>
    </b:Interaction.Triggers>
    <TextBlock Foreground="{DynamicResource SuperBrightTextColor}">Pilots</TextBlock>
</ToggleButton>

<!-- ATC toggle — visible only when a network is selected -->
<ToggleButton Style="{DynamicResource MapToggleButton}"
              IsChecked="{Binding IsShowAtc}"
              Visibility="{Binding ActiveNetwork, Converter={StaticResource NetworkTypeToVisConverter}}">
    <b:Interaction.Triggers>
        <b:EventTrigger EventName="Checked">
            <b:InvokeCommandAction Command="{Binding EnableNetworkItemCommand}"/>
        </b:EventTrigger>
        <b:EventTrigger EventName="Unchecked">
            <b:InvokeCommandAction Command="{Binding DisableNetworkItemCommand}"/>
        </b:EventTrigger>
    </b:Interaction.Triggers>
    <TextBlock Foreground="{DynamicResource SuperBrightTextColor}">ATC</TextBlock>
</ToggleButton>
```

> **Note:** This requires two value converters: `EnumToBoolConverter` (enum value → bool for `IsChecked`) and `NetworkTypeToVisConverter` (`NetworkType.None` → `Collapsed`, else `Visible`). These are created in Step 2.

- [ ] **Step 2: Add the `dataTypes` namespace to `LiveView.xaml`**

In the `<UserControl>` opening tag, add:

```xaml
xmlns:dataTypes="clr-namespace:FSTRaK.DataTypes"
```

- [ ] **Step 3: Create `EnumToBoolConverter` and `NetworkTypeToVisConverter`**

Create `FSTRaK/Utils/EnumToBoolConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace FSTRaK.Utils
{
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string paramStr && value != null)
                return value.ToString() == paramStr;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
```

Create `FSTRaK/Utils/NetworkTypeToVisConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FSTRaK.DataTypes;

namespace FSTRaK.Utils
{
    public class NetworkTypeToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is NetworkType n && n != NetworkType.None ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
        }
}
```

Add both files to the project in Solution Explorer.

- [ ] **Step 4: Register converters in `LiveView.xaml` resources**

Inside `<UserControl.Resources><ResourceDictionary>`, add:

```xaml
<utils:EnumToBoolConverter x:Key="EnumToBoolConverter"/>
<utils:NetworkTypeToVisConverter x:Key="NetworkTypeToVisConverter"/>
```

- [ ] **Step 5: Update VATSIM map layer visibility bindings**

The existing VATSIM map layers are bound to `IsShowVatsimAircraft`, `IsShowVatsimAirports`, `IsShowVatsimFirs`. These still work via the backing properties driven by `IsShowPilots`/`IsShowAtc`. No change needed here — skip this step if bindings are already on the existing properties.

- [ ] **Step 6: Add IVAO aircraft map layer**

Find the `<!-- Vatsim Aircraft -->` `MapItemsControl` block. Add a new IVAO aircraft block immediately after it, following the same structure:

```xaml
<!-- IVAO Aircraft -->
<map:MapItemsControl ItemsSource="{Binding IvaoAircraftList}"
                     Visibility="{Binding IsShowPilots, Converter={StaticResource BoolToVis}}">
    <map:MapItemsControl.ItemContainerStyle>
        <Style TargetType="map:MapItem">
            <Setter Property="Location" Value="{Binding Location}"/>
        </Style>
    </map:MapItemsControl.ItemContainerStyle>
    <map:MapItemsControl.ItemTemplate>
        <DataTemplate>
            <map:MapPanel>
                <Path Data="{Binding Icon, Converter={StaticResource StringToGeometryConverter}}"
                      Fill="{DynamicResource IvaoAircraftFillBrush}"
                      Stroke="{DynamicResource IvaoAircraftStrokeBrush}"
                      StrokeThickness="0.5"
                      RenderTransformOrigin="0.5,0.5">
                    <Path.RenderTransform>
                        <RotateTransform Angle="{Binding Heading}"/>
                    </Path.RenderTransform>
                    <Path.ToolTip>
                        <StackPanel>
                            <TextBlock Text="{Binding Callsign}" FontWeight="Bold"/>
                            <TextBlock>
                                <Run Text="{Binding Departure}"/><Run Text=" → "/><Run Text="{Binding Destination}"/>
                            </TextBlock>
                            <TextBlock Text="{Binding Aircraft}"/>
                            <TextBlock>
                                <Run Text="ALT: "/><Run Text="{Binding Altitude}"/>
                                <Run Text=" GS: "/><Run Text="{Binding Groundspeed}"/>
                            </TextBlock>
                        </StackPanel>
                    </Path.ToolTip>
                </Path>
            </map:MapPanel>
        </DataTemplate>
    </map:MapItemsControl.ItemTemplate>
</map:MapItemsControl>
```

- [ ] **Step 7: Add IVAO ATC map layer**

Add after the IVAO Aircraft block. The ATC layer renders polygons (if available) and icons. Follow the VATSIM airport pattern:

```xaml
<!-- IVAO ATC polygons -->
<map:MapItemsControl ItemsSource="{Binding IvaoAtcList}"
                     Visibility="{Binding IsShowAtc, Converter={StaticResource BoolToVis}}">
    <map:MapItemsControl.ItemTemplate>
        <DataTemplate>
            <map:MapPanel>
                <map:MapPolygon Locations="{Binding ControlPolygon}"
                                Fill="{DynamicResource VatsimTraconFillBrush}"
                                Stroke="{DynamicResource VatsimTraconStrokeBrush}"
                                StrokeThickness="1"
                                Visibility="{Binding ControlPolygon, Converter={StaticResource NullToVisConverter}}"/>
            </map:MapPanel>
        </DataTemplate>
    </map:MapItemsControl.ItemTemplate>
</map:MapItemsControl>

<!-- IVAO ATC icons -->
<map:MapItemsControl ItemsSource="{Binding IvaoAtcList}"
                     Visibility="{Binding IsShowAtc, Converter={StaticResource BoolToVis}}">
    <map:MapItemsControl.ItemContainerStyle>
        <Style TargetType="map:MapItem">
            <Setter Property="Location" Value="{Binding Location}"/>
        </Style>
    </map:MapItemsControl.ItemContainerStyle>
    <map:MapItemsControl.ItemTemplate>
        <DataTemplate>
            <map:MapPanel>
                <StackPanel>
                    <TextBlock Text="{Binding Callsign}"
                               Foreground="{DynamicResource SuperBrightTextColor}"
                               FontSize="10" FontWeight="Bold">
                        <TextBlock.ToolTip>
                            <StackPanel>
                                <TextBlock Text="{Binding Callsign}" FontWeight="Bold"/>
                                <TextBlock Text="{Binding DisplayName}"/>
                                <TextBlock Text="{Binding Frequency}"/>
                            </StackPanel>
                        </TextBlock.ToolTip>
                    </TextBlock>
                </StackPanel>
            </map:MapPanel>
        </DataTemplate>
    </map:MapItemsControl.ItemTemplate>
</map:MapItemsControl>
```

> **Note:** `NullToVisConverter` — check if this already exists in the project. If not, add a simple one to `Utils/`:
> ```csharp
> public class NullToVisConverter : IValueConverter
> {
>     public object Convert(object value, Type t, object p, CultureInfo c)
>         => value != null ? Visibility.Visible : Visibility.Collapsed;
>     public object ConvertBack(object value, Type t, object p, CultureInfo c)
>         => Binding.DoNothing;
> }
> ```

- [ ] **Step 8: Add IVAO brush resources**

Open `FSTRaK/Resources/` and find where VATSIM brushes are defined (search for `VatsimAircraftFillBrush` in the resource dictionaries). Add IVAO equivalents nearby:

```xaml
<SolidColorBrush x:Key="IvaoAircraftFillBrush" Color="#FF8C00"/>
<SolidColorBrush x:Key="IvaoAircraftStrokeBrush" Color="#FF6600"/>
```

- [ ] **Step 9: Build the solution**

`Build → Build Solution`. Expected: clean build. Fix any XAML binding or converter errors.

- [ ] **Step 10: Commit**

```bash
git add FSTRaK/Views/LiveView.xaml
git add FSTRaK/Utils/EnumToBoolConverter.cs
git add FSTRaK/Utils/NetworkTypeToVisConverter.cs
git commit -m "feat: update live map UI with network selector and IVAO layers"
```

---

## Task 7: Update README.md

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace Features and Roadmap sections**

Open `README.md`. Replace the existing `## Features` section with:

```markdown
## Features
* Automatic silent start-up.
* Automatic flight tracking (hands-free experience).
* Option (default) to save only complete flights - i.e. flight ended in parking state, with engines off and parking brake set, after having flown.
* Multiple map providers including FAA ArcGIS charts, OpenAIP, SkyVector, OpenTopoMap, Bing, MapTiler, and more.
* Flight analysis and scoring.
* Live flight tracking with a moving map.
* **VATSIM and IVAO live network support** — view pilots and ATC on the live map.
* Statistics (most used aircraft, most used airlines, average and max payload, distance, etc.)
* Dark mode.
```

Replace the existing `## Roadmap` section with:

```markdown
## Roadmap
- [ ] Simbrief integration (fetch passengers, planned vs actual fuel and time, planned vs actual route).
- [ ] More statistics.
- [ ] Display bearing/distance to a designated point on the map.
- [x] VATSIM integration (display live traffic and ATC on the map).
- [x] IVAO integration (display live traffic and ATC on the map).
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: update README for VATSIM + IVAO support"
```

---

## Task 8: Manual smoke test and final cleanup

Since automated tests cannot run in this Mac environment, perform a structured manual test on Windows.

- [ ] **Step 1: Build in Release|x64 and run**

Build the solution in `Release|x64`. Launch the app.

- [ ] **Step 2: Test VATSIM network**

1. Go to Live map view
2. Click "VATSIM" selector — confirm Pilots and ATC toggles appear
3. Enable Pilots — confirm VATSIM aircraft appear on map after ~5 seconds
4. Enable ATC — confirm VATSIM airports and FIRs appear
5. Disable Pilots — confirm aircraft disappear; ATC stays visible
6. Disable ATC — confirm ATC disappears; service should stop (no active toggles)
7. Click "VATSIM" again — confirm toggles disappear and network is deselected

- [ ] **Step 3: Test IVAO network**

1. Click "IVAO" selector — confirm Pilots and ATC toggles appear
2. Enable Pilots — confirm IVAO aircraft appear on map
3. Enable ATC — confirm IVAO ATC items appear
4. Check tooltip on an ATC item — confirm callsign, frequency, logon time, ATIS

- [ ] **Step 4: Test network switch**

1. Enable VATSIM with Pilots on
2. Click "IVAO" — confirm VATSIM aircraft cleared, IVAO selected, toggles reset to off

- [ ] **Step 5: Test Settings**

1. Go to Settings — confirm IVAO ID field appears below VATSIM ID
2. Enter an IVAO ID and restart — confirm the value persists

- [ ] **Step 6: Commit any fixes, then tag**

```bash
git add -p   # stage only intentional fixes
git commit -m "fix: <describe what you fixed>"
```

---

## Task 9: Update flightsim.to listing

This is a manual step — no code changes.

- [ ] **Step 1: Log in to flightsim.to and open the FSTRaK addon page editor**

- [ ] **Step 2: Replace the listing description with the updated copy**

The full updated copy is in the design spec at:
`docs/superpowers/specs/2026-03-30-ivao-network-support-design.md`
under the **"Full updated listing copy"** section.

- [ ] **Step 3: Save and publish**

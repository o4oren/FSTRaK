# FSTRaK — Source Tree Analysis

## Repository Root

```
FSTRaK/                              # Repository root
├── FSTRaK.sln                       # Visual Studio solution file
├── CLAUDE.md                        # AI coding assistant context (architecture docs)
├── CLAUDE-CHANGES.md                # Change documentation
├── README.md                        # Project overview, features, screenshots
│
├── FSTRaK/                          # Main application project ★
│   ├── FSTrAk.csproj               # Project file (.NET Framework 4.7.2, x64)
│   ├── FSTrAk.ico                  # Application icon
│   ├── App.xaml                    # WPF application definition (entry point)
│   ├── App.xaml.cs                 # Startup: logging, DB warmup, settings, theme
│   ├── App.config                  # EF6/SQLite provider config, connection string, settings
│   ├── RelayCommand.cs             # ICommand implementation for MVVM
│   │
│   ├── BusinessLogic/              # ★ Core application logic
│   │   ├── SimconnectService/
│   │   │   └── SimConnectService.cs    # Singleton — SimConnect SDK facade, 50ms polling
│   │   │
│   │   ├── FlightManager/
│   │   │   ├── FlightManager.cs        # Singleton — flight state machine owner
│   │   │   └── State/                  # State Pattern implementation
│   │   │       ├── IFlightManagerState.cs       # State interface
│   │   │       ├── AbstractState.cs             # Base state (event helpers, timing)
│   │   │       ├── SimNotInFlightState.cs       # Idle — waiting for flight
│   │   │       ├── FlightStartedState.cs        # Flight initiated — creates entities
│   │   │       ├── TaxiOutState.cs              # Ground movement pre-takeoff
│   │   │       ├── TakeoffRollState.cs          # Runway acceleration
│   │   │       ├── FlightState.cs               # Airborne — envelope monitoring
│   │   │       ├── LandedState.cs               # Touchdown — landing quality scoring
│   │   │       ├── TaxiInState.cs               # Ground movement post-landing
│   │   │       ├── FlightEndedState.cs          # Flight complete — save to DB
│   │   │       └── CrashedState.cs              # Crash detected → ends flight
│   │   │
│   │   └── VatsimService/
│   │       ├── VatsimService.cs             # Singleton — VATSIM API polling (60s)
│   │       └── VatsimModel/
│   │           ├── VatsimData.cs            # API response: pilots, controllers, ATIS
│   │           ├── VatsimStaticData.cs      # Parsed VATSpy.dat: countries, airports, FIRs, UIRs
│   │           ├── Pilot.cs                 # Online pilot data
│   │           ├── Controller.cs            # Online controller data
│   │           ├── Atis.cs                  # ATIS broadcast data
│   │           ├── FlightPlan.cs            # Filed flight plan
│   │           └── BoundariesGeoJson.cs     # GeoJSON FIR boundary model
│   │
│   ├── Models/                     # ★ Domain entities and data access
│   │   ├── BaseModel.cs                # INotifyPropertyChanged base
│   │   ├── AirportResolver.cs          # Singleton — loads airports.csv into dictionary
│   │   ├── SQLiteConfiguration.cs      # EF6 SQLite provider registration
│   │   └── Entity/
│   │       ├── LogbookContext.cs        # DbContext — Flights, FlightEvents, Aircraft
│   │       ├── Flight.cs               # Root aggregate — flight record with scoring
│   │       ├── Aircraft.cs             # Aircraft entity (title, type, manufacturer)
│   │       ├── Airport.cs              # Airport data model (from CSV)
│   │       ├── Migrations/
│   │       │   └── Configuration.cs    # EF6 auto-migration config + Seed method
│   │       └── FlightEvent/            # TPH event hierarchy
│   │           ├── BaseFlightEvent.cs       # Base: position, altitude, speed, time
│   │           ├── ScoringEvent.cs          # Abstract: adds ScoreDelta
│   │           ├── FlightStartedEvent.cs
│   │           ├── TakeoffEvent.cs
│   │           ├── LandingEvent.cs          # Landing quality + vertical speed
│   │           ├── TaxiOutEvent.cs
│   │           ├── TaxiInEvent.cs
│   │           ├── ParkingEvent.cs
│   │           ├── FlightEndedEvent.cs
│   │           ├── CrashEvent.cs
│   │           ├── OverspeedEvent.cs
│   │           ├── FlapsSpeedExceededEvent.cs
│   │           ├── GearspeedExceededEvent.cs
│   │           └── StallWarningEvent.cs
│   │
│   ├── DataTypes/                  # Enums, structs, constants
│   │   ├── Consts.cs                   # Conversion constants, image resource names
│   │   ├── FlightOutcome.cs            # Unknown, Completed, Crashed, Exited
│   │   ├── FlightParams.cs             # Real-time telemetry struct
│   │   ├── LandingRate.cs              # Soft, Fair, Good, Perfect, Hard, NotSet
│   │   ├── NearestAirportRequestType.cs # Departure, Arrival, CrashedNear
│   │   ├── Settings.cs                 # Units enum, TimePeriod enum
│   │   └── SimConnectDataTypes.cs      # SimConnect enums + marshaled structs
│   │
│   ├── ViewModels/                 # ★ MVVM presentation logic
│   │   ├── BaseViewModel.cs            # INotifyPropertyChanged base
│   │   ├── MainWindowViewModel.cs      # Navigation controller (4 views)
│   │   ├── LiveViewViewModel.cs        # Live map, flight path, VATSIM overlays
│   │   ├── LogbookViewModel.cs         # Flight list, search, async loading
│   │   ├── StatisticsViewModel.cs      # Aggregate flight metrics
│   │   ├── SettingsViewModel.cs        # Settings two-way binding
│   │   ├── FlightDetailsViewModel.cs   # Flight replay, scoring, chart data
│   │   ├── FlightDetailsParamsViewModel.cs # Flight telemetry display
│   │   ├── AddCommentViewModel.cs      # Comment editor
│   │   └── EditAircraftViewModel.cs    # Aircraft correction
│   │
│   ├── Views/                      # ★ XAML UI definitions
│   │   ├── MainWindow.xaml(.cs)        # Top-level window + navigation
│   │   ├── LiveView.xaml(.cs)          # Real-time tracking map
│   │   ├── LogbookView.xaml(.cs)       # Flight history
│   │   ├── StatisticsView.xaml(.cs)    # Statistics dashboard
│   │   ├── SettingsView.xaml(.cs)      # Preferences
│   │   ├── FlightDetailsView.xaml(.cs) # Flight replay + charts
│   │   ├── FlightDetailsParamsView.xaml(.cs) # Telemetry panel
│   │   ├── OverlayTextCardControl.xaml(.cs)  # Reusable overlay card
│   │   ├── AddCommentPopupView.xaml(.cs)     # Comment dialog
│   │   └── EditAircraftPopupView.xaml(.cs)   # Aircraft editor dialog
│   │
│   ├── Utils/                      # Helpers and utilities
│   │   ├── AircraftResolver.cs         # Aircraft manufacturer/type normalization
│   │   ├── MapProviderResolver.cs      # Map provider from settings
│   │   ├── SkyVectorMapTileLayer.cs    # SkyVector VFR/IFR tiles
│   │   ├── SkyVectorTileSource.cs      # AIRAC cycle fetching
│   │   ├── MapTilerMapTileLayer.cs     # MapTiler tiles
│   │   ├── AzureMapsMapTileLayer.cs    # Azure Maps tiles
│   │   ├── MapUtils.cs                 # Antimeridian wrapping
│   │   ├── CoordinatesUtil.cs          # Geographic centroid
│   │   ├── PathUtil.cs                 # App data directory path
│   │   ├── ResourceUtil.cs             # Theme/font switching
│   │   ├── Converters.cs               # WPF value converters
│   │   ├── HyperlinkText.cs            # Markdown-to-hyperlink behavior
│   │   ├── CollectionUtils.cs          # Collection helpers
│   │   ├── ColorUtil.cs                # Color conversion
│   │   ├── MathUtils.cs                # Clamp function
│   │   ├── StringUtil.cs               # String manipulation
│   │   ├── TimeUtils.cs                # Elapsed time calculation
│   │   └── UnitsUtil.cs                # Imperial/metric conversion
│   │
│   ├── Resources/                  # Static assets
│   │   ├── Theme.xaml                  # Light theme (colors, brushes, styles)
│   │   ├── DarkTheme.xaml              # Dark theme overlay
│   │   ├── ButtonsTheme.xaml           # Button styles
│   │   ├── Images.xaml                 # Image resource definitions
│   │   ├── AircraftIconsDictionary.xaml # Aircraft icon geometries
│   │   ├── MapProvidersDictionary.xaml # Map tile provider configs
│   │   ├── Fonts/
│   │   │   └── Slopes.ttf             # Custom font
│   │   ├── Data/
│   │   │   ├── airports.csv            # 12MB airport database
│   │   │   ├── VATSpy.dat             # VATSIM facility data
│   │   │   └── Boundaries.geojson     # FIR/TMA boundary polygons
│   │   └── Images/
│   │       ├── FSTrAk.ico             # App icon
│   │       ├── VATSIM_Logo_*.png      # VATSIM branding
│   │       ├── tower-64.png           # ATC facility icons
│   │       ├── radar-64.png
│   │       ├── antenna-radar-64.png
│   │       ├── radio-antenna-64.png
│   │       ├── tower-radar-64.png
│   │       └── Aircraft/003884/       # Aircraft type images
│   │           ├── A320-image.png
│   │           ├── A330-image.png
│   │           ├── B737-image.png
│   │           ├── B747-image.png
│   │           ├── C172-image.png
│   │           └── ... (13 types total)
│   │
│   ├── Properties/                 # .NET project properties
│   │   ├── AssemblyInfo.cs
│   │   ├── Resources.resx / .Designer.cs
│   │   ├── Settings.settings / .Designer.cs
│   │   └── DataSources/           # VS data source bindings
│   │
│   ├── lib/                        # Native SQLite interop DLLs
│   │   ├── x64/SQLite.Interop.dll
│   │   └── x86/SQLite.Interop.dll
│   │
│   ├── Microsoft.FlightSimulator.SimConnect.dll  # SimConnect managed wrapper
│   └── SimConnect.dll                            # SimConnect native DLL
│
├── Setup/                          # MSI installer project
│   └── Setup.vdproj
│
├── docs/                           # Project documentation (this folder)
├── _bmad/                          # BMAD method tooling
└── _bmad-output/                   # BMAD workflow outputs
```

## Critical Folders

| Folder | Purpose | Key Files |
|--------|---------|-----------|
| `BusinessLogic/SimconnectService/` | MSFS connection, data polling | `SimConnectService.cs` (singleton) |
| `BusinessLogic/FlightManager/` | Flight lifecycle management | `FlightManager.cs`, `State/*.cs` |
| `BusinessLogic/VatsimService/` | Live VATSIM network integration | `VatsimService.cs`, `VatsimModel/*.cs` |
| `Models/Entity/` | EF6 entities and DB context | `LogbookContext.cs`, `Flight.cs`, `Aircraft.cs` |
| `Models/Entity/FlightEvent/` | Flight event TPH hierarchy | 14 event types |
| `ViewModels/` | MVVM presentation logic | 10 ViewModels |
| `Views/` | XAML UI | 10 Views (4 primary + details + popups) |
| `Utils/` | Helpers | Map tiles, converters, aircraft resolution |
| `Resources/Data/` | Static data files | airports.csv (12MB), VATSpy.dat, Boundaries.geojson |

## Entry Points

- **Application Entry:** `App.xaml` → `App.xaml.cs` (`OnApplicationStart`)
- **Main Window:** `Views/MainWindow.xaml` (StartupUri)
- **SimConnect Init:** `SimConnectService.Initialize()` (called after window loaded)
- **DB Init:** `LogbookContext` constructor (auto-migration on first use)

## File Counts

| Category | Count |
|----------|-------|
| C# source files (.cs) | ~55 |
| XAML files (.xaml) | ~16 |
| Resource dictionaries | 6 |
| Data files (CSV, GeoJSON, DAT) | 3 |
| Image assets | ~20 |
| Native DLLs | 4 |

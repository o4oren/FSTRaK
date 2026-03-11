# FSTRaK — Architecture

## Executive Summary

FSTRaK is a WPF desktop application (.NET Framework 4.7.2, C#, x64) that automatically tracks and logs flights in Microsoft Flight Simulator (MSFS 2020 and 2024). The architecture follows MVVM for the presentation layer with a State Pattern governing the flight lifecycle in the business logic layer. Data is persisted locally in SQLite via Entity Framework 6.

## Architecture Pattern

**MVVM + State Machine + Singleton Services**

```
┌─────────────────────────────────────────────────────────┐
│                    WPF Views (XAML)                       │
│  MainWindow │ LiveView │ LogbookView │ Settings │ Stats  │
├─────────────────────────────────────────────────────────┤
│                   ViewModels (C#)                         │
│  MainWindowVM │ LiveViewVM │ LogbookVM │ SettingsVM │ …  │
├─────────────────────────────────────────────────────────┤
│                  Business Logic Layer                     │
│  ┌──────────────┐  ┌─────────────┐  ┌──────────────┐   │
│  │ SimConnect    │  │  Flight     │  │   Vatsim     │   │
│  │ Service       │→ │  Manager    │  │   Service    │   │
│  │ (50ms poll)   │  │  (State     │  │   (60s poll) │   │
│  │               │  │   Machine)  │  │              │   │
│  └──────────────┘  └─────────────┘  └──────────────┘   │
├─────────────────────────────────────────────────────────┤
│                    Data Layer                             │
│  ┌──────────────┐  ┌─────────────┐  ┌──────────────┐   │
│  │ LogbookContext│  │  Airport    │  │  Properties  │   │
│  │ (EF6/SQLite) │  │  Resolver   │  │  .Settings   │   │
│  └──────────────┘  └─────────────┘  └──────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Core Components

### SimConnectService

**Type:** Thread-safe singleton
**Responsibility:** Facade over the SimConnect SDK

- Polls flight data from MSFS every 50ms via a timer
- Reconnects every 10 seconds when disconnected
- Detects MSFS version (2020 vs 2024) via `RequestFacilitiesList_EX1` version field
- Receives SimConnect messages via a Win32 `HwndSource` hook on the main WPF window
- Exposes all data via `INotifyPropertyChanged` properties

**Key Properties:**
- `IsConnected` — SimConnect connection state
- `SimVersion` — "MSFS2020" or "MSFS2024"
- `IsInFlight` — Whether the simulator is in flight mode
- `CameraState` — Current camera view (25+ modes)
- `FlightData` — Real-time telemetry (position, speeds, altitude, etc.)
- `AircraftData` — Loaded aircraft metadata

**Constraints:**
- Must be initialized **after** the main WPF window is loaded (requires window handle)
- x64-only (native SimConnect DLL dependency)

### FlightManager

**Type:** Thread-safe singleton
**Responsibility:** Domain model managing the flight lifecycle

- Subscribes to `SimConnectService.PropertyChanged` events
- Manages the flight state machine (10 states)
- Creates and populates `Flight` entities during the flight
- Triggers flight persistence when the flight ends

**Flight State Machine:**

```
                    ┌─── CrashedState ←── (any movement state)
                    ↓
SimNotInFlightState → FlightStartedState → TaxiOutState → TakeoffRollState
                                                                  ↓
                    FlightEndedState ← TaxiInState ← LandedState ← FlightState
                         ↑                  │              ↑           │
                         │                  └──────────────┘           │
                         │                  (go-around)                │
                         └─────────────────────────────────────────────┘
                                        (landed again)
```

**State Transition Triggers:**
| From → To | Trigger |
|-----------|---------|
| SimNotInFlight → FlightStarted | Sim enters flight mode |
| FlightStarted → TaxiOut | Position changes + ground velocity > 1 kt |
| TaxiOut → TakeoffRoll | Ground velocity > 40 kts + throttle > 75% |
| TakeoffRoll → Flight | Aircraft leaves ground |
| Flight → Landed | Aircraft touches ground |
| Landed → Flight | Aircraft leaves ground again (go-around) |
| Landed → TaxiIn | Ground velocity < 35 kts + throttle < 50% |
| TaxiIn → FlightEnded | Parked (vel < 2 kts + brake set + engines off) |
| TaxiIn → TakeoffRoll | Ground velocity > 40 kts (rejected landing) |
| Any movement → Crashed | Crash flag detected |

**Scoring System:**
Starting score of 100, modified by events:
- Landing: -35 (Hard), -10 (Fair/Soft), 0 (Good), +10 (Perfect)
- Overspeed, stall, flaps exceeded, gear exceeded: negative deltas
- Final score clamped to [0, 110]

### VatsimService

**Type:** Thread-safe singleton
**Responsibility:** Real-time VATSIM network integration

- Polls `https://data.vatsim.net/v3/vatsim-data.json` every 60 seconds
- Parses static data (`VATSpy.dat`) at startup for airport/FIR/UIR metadata
- Loads `Boundaries.geojson` for FIR polygon rendering
- Provides pilot positions, controller frequencies, and ATIS broadcasts

### Data Model

**Entity Framework 6 with SQLite (Code First, automatic migrations)**

```
┌─────────────────┐     1:N     ┌──────────────────────┐
│     Flight      │────────────→│    BaseFlightEvent    │
│─────────────────│             │──────────────────────│
│ Id              │             │ Id                    │
│ DepartureAirport│             │ Time                  │
│ ArrivalAirport  │             │ Latitude, Longitude   │
│ StartTime       │             │ Altitude              │
│ EndTime         │             │ IndicatedAirspeed     │
│ FlightTimeMilis │             │ GroundSpeed           │
│ FlightDistanceNm│             │ TrueHeading           │
│ TotalFuelUsed   │             │ GroundAltitude        │
│ TotalPayloadLbs │             │ FlightId (FK)         │
│ FlightOutcome   │             │ Discriminator (TPH)   │
│ Score           │             └──────────┬───────────┘
│ LandingFpm      │                        │ TPH Subtypes:
│ Comment         │             ┌──────────┴───────────┐
│ Aircraft (FK)   │             │ FlightStartedEvent   │
└────────┬────────┘             │ TakeoffEvent         │
         │ N:1                  │ LandingEvent         │
┌────────┴────────┐             │ TaxiOutEvent         │
│    Aircraft     │             │ TaxiInEvent          │
│─────────────────│             │ ParkingEvent         │
│ Id              │             │ FlightEndedEvent     │
│ Title (unique)  │             │ CrashEvent           │
│ LiveryName      │             │ OverspeedEvent       │
│ AircraftType    │             │ FlapsSpeedExceeded   │
│ Category        │             │ GearspeedExceeded    │
│ Manufacturer    │             │ StallWarningEvent    │
│ Airline         │             └──────────────────────┘
│ Model           │
│ TailNumber      │
│ NumberOfEngines  │
│ EngineType      │
│ EmptyWeightLbs  │
└─────────────────┘
```

**Key design decisions:**
- **TPH (Table Per Hierarchy)** for flight events — all event types stored in one `FlightEvent` table with a discriminator column
- **Automatic migrations** with `AutomaticMigrationDataLossAllowed = true` for schema flexibility
- **Seed method** handles data fixes across version upgrades
- **Lazy loading** enabled for flight events (loaded on demand)

### MSFS Version Detection

FSTRaK supports both MSFS 2020 and MSFS 2024:
- **Detection:** Uses `RequestFacilitiesList_EX1` version field to determine which MSFS is running
- **2020 differences:** Aircraft identified by parsing the `.air` file path (`EnrichAircraftDataFromFile`)
- **2024 differences:** Aircraft identified via `LiveryName` SimVar; different airport data struct layout

### Map System

Uses `XAML.MapControl.WPF` (v13.4) with multiple tile providers:
- **Built-in:** OpenStreetMap, Bing Maps
- **Custom:** SkyVector VFR/IFR (fetches AIRAC cycle), MapTiler, Azure Maps
- **Caching:** SQLite tile cache via `XAML.MapControl.SQLiteCache`
- **Antimeridian handling:** Custom wrapping logic in `MapUtils.cs`

## Data Flow

### Real-Time Flight Tracking
```
MSFS → SimConnect SDK → SimConnectService (50ms) → FlightManager (state machine)
    → LiveViewViewModel (INotifyPropertyChanged) → LiveView (XAML binding)
```

### Flight Persistence
```
FlightEndedState → Flight.UpdateScore() → LogbookContext.SaveChanges()
    → SQLite DB → LogbookViewModel (query) → LogbookView
```

### VATSIM Overlay
```
VATSIM API (60s) → VatsimService → LiveViewViewModel → LiveView (map overlays)
VATSpy.dat + Boundaries.geojson → Static data (parsed once at startup)
```

## Threading Model

- **UI Thread:** WPF dispatcher, all UI updates
- **SimConnect Messages:** Delivered via Win32 `HwndSource` on the UI thread
- **Timers:** `System.Timers.Timer` for SimConnect polling (50ms) and reconnection (10s)
- **Background Tasks:** DB warmup, airport CSV loading, VATSIM polling
- **Dispatcher marshalling:** `Application.Current.Dispatcher.Invoke()` for cross-thread UI updates

## Security and Privacy

- **Local-only data:** All flight data stored in local SQLite database
- **No authentication:** No user accounts or server-side storage
- **API keys:** Bing Maps, MapTiler, Azure Maps keys stored in user settings (local)
- **VATSIM CID:** Optional, stored locally for network overlay

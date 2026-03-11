# FSTRaK — State Management Patterns

## Overview

FSTRaK uses property-change notification (`INotifyPropertyChanged`) as its primary state management mechanism, consistent with the WPF MVVM pattern. There is no external state management library (Redux, MobX, etc.) — state flows through observable properties and event subscriptions.

## State Management Layers

### 1. Application Settings (Persistent)

**Mechanism:** `Properties.Settings.Default` (WPF User Settings)

Settings are persisted across sessions via the standard .NET settings infrastructure. An upgrade flag (`UpgradeRequired`) triggers migration on first run after version update.

**Key settings:**
- `MapTileProvider` — Active map provider (OpenStreetMap, SkyVector, MapTiler, etc.)
- `BingApiKey`, `MapTilerApiKey` — API keys for map providers
- `IsAlwaysOnTop`, `IsStartMinimized`, `IsMinimizeToTray`, `IsStartAutomatically` — Window behavior
- `IsSaveOnlyCompleteFlights` — Whether to save incomplete flights
- `Units` — Imperial (0) or Metric (1)
- `Theme` — Normal or Dark
- `FontName` — Application font
- `VatsimId` — User's VATSIM CID
- `Top`, `Left`, `Width`, `Height` — Window position and size

### 2. Flight State Machine (Runtime — Core Domain)

**Mechanism:** State Pattern with `IFlightManagerState` interface

The flight lifecycle is governed by a state machine with 10 states:

```
SimNotInFlightState
    → FlightStartedState (sim enters flight mode)
        → TaxiOutState (aircraft moves)
            → TakeoffRollState (speed > 40kts + throttle > 75%)
                → FlightState (aircraft leaves ground)
                    → LandedState (aircraft touches down)
                        → FlightState (go-around)
                        → TaxiInState (speed < 35kts)
                            → TakeoffRollState (rejected landing, go-around)
                            → FlightEndedState (parked, engines off, brake set)
                                → FlightStartedState (engines restart)
    CrashedState (from any movement state) → FlightEndedState
```

**State data flow:**
1. `SimConnectService` polls MSFS every 50ms via native SimConnect
2. `FlightManager` subscribes to `SimConnectService.PropertyChanged`
3. Current state's `ProcessFlightData()` evaluates transition conditions
4. State transitions update `FlightManager.State` property
5. ViewModels observe state changes via `INotifyPropertyChanged`

### 3. Singleton Services (Runtime — Global)

**SimConnectService** — Thread-safe singleton (double-checked locking)
- Owns the SimConnect connection lifecycle
- Exposes: `IsConnected`, `SimVersion`, `IsInFlight`, `CameraState`, `FlightData`, `AircraftData`
- Reconnects every 10 seconds when disconnected

**FlightManager** — Thread-safe singleton (double-checked locking)
- Owns the flight state machine and active flight
- Exposes: `ActiveFlight`, `CurrentFlightParams`, `State`, `SimConnectInFlight`, `SimConnectIsConnected`

**VatsimService** — Thread-safe singleton (double-checked locking)
- Polls VATSIM API every 60 seconds
- Exposes: `VatsimData` (pilots, controllers, ATIS)

**AirportResolver** — Thread-safe singleton
- Loads `airports.csv` at startup into in-memory dictionary
- Provides airport lookup by ICAO code

### 4. ViewModel State (Runtime — UI)

Each ViewModel maintains its own UI state via observable properties:

- **LiveViewViewModel**: `FlightPath` (ObservableCollection), `Location`, `Heading`, `MapCenter`, `ZoomLevel`, VATSIM overlay toggles
- **LogbookViewModel**: `SelectedFlight`, flight list, search/filter state
- **FlightDetailsViewModel**: `FlightPath`, `MarkerList`, `ScoreboardText`, chart data
- **StatisticsViewModel**: Aggregated flight metrics
- **SettingsViewModel**: Mirrors `Properties.Settings` for two-way binding

### 5. Database State (Persistent)

**Mechanism:** Entity Framework 6 + SQLite

Three entity sets persisted:
- `Flights` — Flight records with metadata (airports, times, distances, score)
- `FlightEvents` — TPH hierarchy of flight events (takeoff, landing, crash, etc.) with GPS data
- `Aircraft` — Aircraft catalog resolved from SimConnect data

Automatic migrations via `MigrateDatabaseToLatestVersion`. The `Seed` method handles data fixes across version upgrades.

## Data Flow Summary

```
MSFS (SimConnect SDK)
    ↓ [50ms polling]
SimConnectService (singleton)
    ↓ [INotifyPropertyChanged]
FlightManager (singleton + state machine)
    ↓ [INotifyPropertyChanged]
ViewModels (per-view state)
    ↓ [WPF data binding]
Views (XAML UI)

VatsimService (60s polling) → LiveViewViewModel → LiveView
AirportResolver (startup load) → Flight entities, LiveView
Properties.Settings → SettingsViewModel ↔ SettingsView
LogbookContext (EF6/SQLite) → LogbookViewModel, StatisticsViewModel
```

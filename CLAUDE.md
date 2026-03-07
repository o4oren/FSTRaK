# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FSTRaK is a WPF desktop application (.NET Framework 4.7.2, C#, x64) that automatically tracks and logs flights in Microsoft Flight Simulator (MSFS 2020 and 2024). It connects silently to MSFS via SimConnect, detects flights automatically, and persists completed flights to a local SQLite database.

## Building

Open `FSTRaK.sln` in Visual Studio and build with the `x64` platform target (required — x86/AnyCPU will not work due to SimConnect's native DLL dependency). Build configurations: `Debug|x64` and `Release|x64`.

There is no CLI build script; this project relies on MSBuild/Visual Studio tooling on Windows.

## Architecture

### Layers

**BusinessLogic** — core application logic:
- `SimconnectService/SimConnectService.cs` — singleton facade over the SimConnect SDK. Polls flight data every 50ms, reconnects every 10s if disconnected. Detects MSFS version (2020 vs 2024) via `RequestFacilitiesList_EX1` version field. Exposes properties via `INotifyPropertyChanged`.
- `FlightManager/FlightManager.cs` — singleton domain model. Subscribes to `SimConnectService` property changes and manages the flight state machine. Exposes the active flight and current position for the live map.
- `FlightManager/State/` — State pattern implementation. States: `SimNotInFlightState` → `FlightStartedState` → `TaxiOutState` → `TakeoffRollState` → `FlightState` → `LandedState` → `TaxiInState` → `FlightEndedState`. `CrashedState` can be entered from any movement state.

**Models/Entity** — Entity Framework 6 entities backed by SQLite:
- `LogbookContext` — `DbContext` with `Flights`, `FlightEvents`, and `Aircraft` sets. Uses automatic EF migrations (`AutomaticMigrationsEnabled = true`).
- `Flight` — root aggregate. Contains a collection of `BaseFlightEvent` (TPH inheritance). Score is calculated in `Flight.UpdateScore()` from scoring events.
- `Aircraft` — linked to flights; resolved/created from SimConnect data during `FlightStartedState`.
- `BaseFlightEvent` and subtypes (Landing, Takeoff, TaxiIn/Out, Parking, Crash, etc.) track GPS position, altitude, speeds, and time at each phase.

**ViewModels** — MVVM pattern. `MainWindowViewModel` owns navigation between four views: Live, Logbook, Statistics, Settings. Each view has a corresponding ViewModel. `BaseViewModel` implements `INotifyPropertyChanged`.

**Utils** — helpers for airports (`AirportResolver` loads `airports.csv` at startup), aircraft resolution, map providers, coordinate math, unit conversion, theming.

### Key Design Points

- Both `SimConnectService` and `FlightManager` are thread-safe singletons (double-checked locking).
- `SimConnectService.Initialize()` must be called after the main WPF window is loaded because SimConnect messages are received via a Win32 `HwndSource` hook on the window handle.
- The data directory for debug builds is `%LOCALAPPDATA%\FSTRaK_DEBUG`; for release it is `%LOCALAPPDATA%\FSTRaK`. This is where the SQLite DB and log files live.
- Settings are persisted via `Properties.Settings` (standard WPF settings). An upgrade flag triggers migration on first run after version update.
- Logging uses Serilog with daily rolling file sink and a Trace sink. Debug builds log at `Debug` level; release at `Information`.
- `FlightEndedState` saves a flight only if it is `Completed` (ended with engines off, parking brake, in a parking spot) **or** if the "save only complete flights" setting is disabled.
- MSFS 2020 aircraft identification reads from the `.air` file via `EnrichAircraftDataFromFile`; MSFS 2024 uses the `LiveryName` SimVar to distinguish liveries of the same title.

### Map

Uses `XAML.MapControl.WPF` (v13.4) for the live map and flight replay. Custom tile layers exist for SkyVector VFR/IFR tiles (`SkyVectorMapTileLayer`) and MapTiler (`MapTilerMapTileLayer`). Map provider options are declared in `Resources/MapProvidersDictionary.xaml`.

### Database Migrations

EF6 automatic migrations run on `LogbookContext` construction. The `Seed` method in `Models/Entity/Migrations/Configuration.cs` handles one-time data fixes across upgrades (e.g., backfilling `LandingFpm`, dropping legacy indexes). `AutomaticMigrationDataLossAllowed = true` is set intentionally for schema evolution flexibility.

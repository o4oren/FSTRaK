# FSTRaK — Development Guide

## Prerequisites

- **Windows 10/11** (required — WPF and SimConnect are Windows-only)
- **Visual Studio 2019+** (with .NET desktop development workload)
- **.NET Framework 4.7.2** Developer Pack
- **Microsoft Flight Simulator** (2020 or 2024) — for runtime testing with SimConnect

## Getting Started

### 1. Clone and Open

```bash
git clone https://github.com/o4oren/FSTRaK.git
```

Open `FSTRaK.sln` in Visual Studio.

### 2. Platform Configuration

**Critical:** Set platform to `x64` before building.

- In Visual Studio: Build → Configuration Manager → Active Solution Platform → `x64`
- x86 and AnyCPU will **not work** due to SimConnect's native x64 DLL dependency

### 3. Build

- **Debug:** `Debug|x64` — logs at Debug level, data stored in `%LOCALAPPDATA%\FSTRaK_DEBUG`
- **Release:** `Release|x64` — logs at Information level, data stored in `%LOCALAPPDATA%\FSTRaK`

NuGet packages restore automatically on build.

### 4. Run

Launch from Visual Studio (F5) or run the built executable directly. MSFS does **not** need to be running — FSTRaK will attempt to connect every 10 seconds until the simulator is detected.

## Project Structure

| Layer | Namespace | Responsibility |
|-------|-----------|---------------|
| BusinessLogic | `FSTRaK.BusinessLogic.*` | SimConnect communication, flight state machine, VATSIM |
| Models | `FSTRaK.Models.*` | Domain entities, EF6 DbContext, airport data |
| ViewModels | `FSTRaK.ViewModels` | MVVM presentation logic |
| Views | `FSTRaK.Views` | XAML UI definitions |
| DataTypes | `FSTRaK.DataTypes` | Enums, structs, SimConnect data definitions |
| Utils | `FSTRaK.Utils` | Helpers (maps, converters, aircraft resolution) |

## Key Development Patterns

### Singletons
`SimConnectService`, `FlightManager`, `VatsimService`, and `AirportResolver` are all thread-safe singletons (double-checked locking). Access via `.Instance`.

### State Machine
The flight lifecycle is governed by states in `BusinessLogic/FlightManager/State/`. To add a new state:
1. Create a class extending `AbstractState`
2. Implement `ProcessFlightData(FlightData data)`
3. Handle transitions by setting `Context.State = new NextState(Context)`

### Entity Framework Migrations
EF6 automatic migrations are enabled (`AutomaticMigrationsEnabled = true`). Schema changes are applied automatically on `LogbookContext` construction. The `Seed` method in `Migrations/Configuration.cs` handles data fixes across upgrades.

### Adding a New Flight Event
1. Create a class in `Models/Entity/FlightEvent/` extending `BaseFlightEvent` or `ScoringEvent`
2. EF6 TPH inheritance will automatically include it in the `FlightEvent` table
3. Add the event creation to the appropriate state's `ProcessFlightData` method
4. Update `FSTrAk.csproj` to include the new file

### Adding a New Map Provider
1. Create a tile layer class in `Utils/` extending `MapTileLayer`
2. Add the provider definition to `Resources/MapProvidersDictionary.xaml`
3. Update `MapProviderResolver.cs` to handle the new provider

## Data Locations

| Build | Path | Contents |
|-------|------|----------|
| Debug | `%LOCALAPPDATA%\FSTRaK_DEBUG\` | `FSTrAk.db` (SQLite), `log.txt` |
| Release | `%LOCALAPPDATA%\FSTRaK\` | `FSTrAk.db` (SQLite), `log.txt` |

## Configuration

Application settings are in `App.config` and `Properties/Settings.settings`:
- **Connection string:** `Data Source=|DataDirectory|FSTrAk.db;Version=3;New=True;Compress=True;`
- **User settings:** Map provider, API keys, window position, theme, units, etc.

## Logging

Serilog with two sinks:
- **File:** Daily rolling log in the data directory (5 files retained)
- **Trace:** Visual Studio Output window

Debug builds log at `Debug` level; release at `Information`.

## Testing

**No test project currently exists.** Manual testing requires MSFS running with SimConnect available.

## Installer

The `Setup/Setup.vdproj` is a Visual Studio Installer Project that builds an MSI package. It requires the Visual Studio Installer Projects extension.

## Common Tasks

| Task | How |
|------|-----|
| Change SimConnect polling rate | Modify `DataInterval` constant in `SimConnectService.cs` |
| Add aircraft type mapping | Update `AircraftResolver.cs` manufacturer/type dictionaries |
| Modify scoring rules | Edit the relevant `ScoringEvent` subclass and `LandedState.cs` |
| Add a new view | Create XAML + ViewModel, add navigation command in `MainWindowViewModel` |
| Update airport data | Replace `Resources/Data/airports.csv` |
| Update VATSIM data | Replace `Resources/Data/VATSpy.dat` and `Boundaries.geojson` |

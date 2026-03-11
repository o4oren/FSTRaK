---
project_name: 'FSTRaK'
user_name: 'Oren'
date: '2026-03-11'
sections_completed: ['technology_stack', 'language_rules', 'framework_rules', 'testing_rules', 'code_quality', 'dev_workflow', 'critical_rules']
status: 'complete'
rule_count: 39
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- **Runtime:** .NET Framework 4.7.2, C# (LangVersion: latest)
- **Platform:** x64 ONLY — x86/AnyCPU will not link against SimConnect
- **UI:** WPF (Windows Presentation Foundation)
- **ORM:** Entity Framework 6.5.1 with automatic migrations
- **Database:** SQLite via System.Data.SQLite 1.0.119
- **Sim Integration:** SimConnect SDK 11.0.62651.3 (native x64 DLL)
- **Mapping:** XAML.MapControl.WPF 13.4.0
- **Charting:** ScottPlot.WPF 4.1.74 (v4 API — do NOT use v5 patterns)
- **Logging:** Serilog 4.2.0 (File + Trace sinks)
- **JSON:** Newtonsoft.Json 13.0.3 (VATSIM), System.Text.Json 9.0.4
- **CSV:** CsvHelper 33.1.0

## Critical Implementation Rules

### C# / .NET Framework Rules

- **Property change notification is mandatory** for any property exposed to UI. Use `OnPropertyChanged()` from `BaseModel` or `BaseViewModel`. Never set backing fields directly if the property drives UI binding.
- **Singleton pattern:** `SimConnectService`, `FlightManager`, `VatsimService`, and `AirportResolver` use double-checked locking via `lock` + `??=`. Follow this exact pattern for new singletons.
- **ObservableCollection reuse:** Never replace an `ObservableCollection` instance that is bound to a UI element. Always `.Clear()` then `.Add()` items. Replacing breaks WPF bindings silently.
- **Dispatcher marshalling:** Any code updating UI-bound properties from a background thread must use `Application.Current.Dispatcher.Invoke()`. Timer callbacks and async completions run off the UI thread.
- **Conditional compilation:** Use `#if DEBUG` for debug-only logging levels. Debug builds define `DEBUG;TRACE`; release defines `TRACE` only.
- **`[NotMapped]` for computed properties:** EF6 entities that expose computed or transient data (e.g., `Airport` lookups, `Location` string) must be marked `[NotMapped]` to prevent SQLite column creation.
- **New source files must be added to `.csproj`:** This is a legacy-style `.csproj` (not SDK-style). New `.cs` files must have a `<Compile Include="..."/>` entry added manually.

### WPF & SimConnect Rules

- **SimConnect initialization timing:** `SimConnectService.Initialize()` MUST be called after `MainWindow.Loaded` event — it requires the window's `HwndSource` handle for Win32 message dispatch. Calling it before the window is loaded will fail silently.
- **SimConnect message loop:** SimConnect events arrive via `WM_USER + 0x0402` on the UI thread's message pump. Never block the UI thread during SimConnect callbacks.
- **MSFS version handling:** Always check `SimConnectService.SimVersion` ("MSFS2020" or "MSFS2024") when dealing with aircraft identification or airport data. The struct layouts differ between versions.
- **MVVM navigation:** `MainWindowViewModel` owns 4 child ViewModels instantiated once in the constructor. Navigation swaps `ActiveView` — do not create new ViewModel instances on navigation.
- **Map tile layers:** Custom tile layers (`SkyVectorMapTileLayer`, `MapTilerMapTileLayer`, `AzureMapsMapTileLayer`) extend `MapTileLayer`. New providers must follow this pattern and be registered in `Resources/MapProvidersDictionary.xaml`.
- **Antimeridian handling:** Any code adding polylines or polygons to the map must use `MapUtils` wrapping logic to prevent 360-degree longitude jumps across the dateline.

### Entity Framework 6 / SQLite Rules

- **Automatic migrations are ON** with `AutomaticMigrationDataLossAllowed = true`. Schema changes apply automatically — no manual migration files needed.
- **TPH (Table Per Hierarchy)** for flight events: All `BaseFlightEvent` subtypes share a single `FlightEvent` table with a discriminator column. New event types just need to extend `BaseFlightEvent` or `ScoringEvent`.
- **Seed method for data fixes:** Version-upgrade data patches go in `Migrations/Configuration.cs` `Seed()` method with idempotent guards. Never put one-time fixes in application startup.
- **Connection string uses `|DataDirectory|`:** The SQLite DB path is set at startup via `AppDomain.CurrentDomain.SetData("DataDirectory", ...)`. Do not hardcode paths.
- **Lazy loading is enabled:** `Flight.FlightEvents` loads on access. Be aware of N+1 query risks when iterating flights.

### Testing Rules

- **No test project exists.** There is no unit test or integration test project in the solution.
- **Manual testing requires MSFS running** with SimConnect available — there is no mock/stub for the SimConnect SDK.
- **If adding a test project:** Use MSTest or xUnit targeting .NET Framework 4.7.2 with x64 platform. The test project must also reference SimConnect DLLs if testing business logic.

### Code Quality & Style Rules

- **Naming conventions:** PascalCase for classes, methods, properties, and public fields. `_camelCase` with underscore prefix for private backing fields. Namespaces mirror folder structure (`FSTRaK.BusinessLogic.FlightManager.State`).
- **File organization:** One primary class per file. File name matches class name. XAML views have matching `.xaml.cs` code-behind files.
- **Logging convention:** Use `Serilog.Log` static methods (`Log.Information()`, `Log.Debug()`, `Log.Error()`). Always pass the exception object as the first argument to `Log.Error(ex, "message")` for structured exception logging.
- **Resource dictionaries:** UI styles, colors, and brushes go in `Resources/Theme.xaml` or `Resources/DarkTheme.xaml`. Do not inline styles in view XAML — use `StaticResource` references.
- **No XML doc comments required** on private/internal members. Only add `<summary>` comments where behavior is non-obvious (e.g., state classes, complex business logic).
- **Error handling:** WPF's `DispatcherUnhandledException` catches unhandled UI thread exceptions and logs them. Do not let exceptions propagate through `INotifyPropertyChanged` chains — they abort the setter silently (see CLAUDE-CHANGES.md for a real example).

### Development Workflow Rules

- **Branch naming:** Feature branches use `major.minor.patch` version format (e.g., `3.1.1`). No strict prefix convention (no `feature/`, `fix/` prefixes observed).
- **Commit messages:** Short imperative descriptions. No conventional-commits format enforced. Keep messages descriptive of the "what" — e.g., "Move aircraft manufacturer/type normalization into AircraftResolver".
- **No CI/CD pipeline:** There is no automated build, test, or deployment pipeline. Builds are done locally in Visual Studio.
- **Release process:** Version is set in `FSTRaK/Properties/AssemblyInfo.cs` (`AssemblyVersion`, `AssemblyFileVersion`). Update both before tagging a release. Settings upgrade logic in `App.xaml.cs` triggers on version change.
- **Single-instance enforcement:** The app uses a named `Mutex` to prevent multiple instances. If you change the app identity or namespace, update the mutex name in `App.xaml.cs`.
- **Data directory convention:** Debug builds use `%LOCALAPPDATA%\FSTRaK_DEBUG`, release uses `%LOCALAPPDATA%\FSTRaK`. This is set once at startup via `AppDomain.SetData("DataDirectory", ...)` and affects the SQLite connection string.

### Critical Don't-Miss Rules

- **ScottPlot v4 API only:** This project uses ScottPlot 4.1.x. The v5 API is completely different (different namespaces, different method signatures). Never use `ScottPlot.Plot` patterns from v5 docs — stick to `formsPlot.Plot.AddScatter()`, `formsPlot.Plot.AddSignal()`, etc.
- **SimConnect struct layout is version-sensitive:** MSFS 2020 and 2024 return different struct fields for aircraft and facility data. Always branch on `SimVersion` when parsing SimConnect responses. Adding a field to a SimConnect struct without matching the sim's layout will corrupt all subsequent fields.
- **Never call `SimConnectService.Initialize()` before window load:** This is the #1 silent failure mode. SimConnect needs the Win32 window handle for message dispatch. Pre-load initialization produces no error but no data will ever arrive.
- **WPF binding failures are silent:** If a property name is misspelled in XAML `{Binding}`, WPF logs a trace warning but does not throw. Always verify bindings work at runtime — the compiler will not catch these.
- **`ObservableCollection` replacement kills bindings:** Assigning a new `ObservableCollection` to a property that WPF is bound to will silently disconnect the UI. Use `.Clear()` + `.Add()` instead. This is a repeat-offender bug pattern.
- **EF6 entity properties without `[NotMapped]` create columns:** Any public property with a getter/setter on an entity class will be mapped to a SQLite column. Computed or transient properties (e.g., resolved airport names) must be marked `[NotMapped]`.
- **Antimeridian polyline wrapping:** Drawing a flight path that crosses the 180° meridian without `MapUtils` wrapping will render a line spanning the entire map horizontally. Always use the wrapping utility for any polyline/polygon added to the map.
- **Thread safety on UI-bound properties:** Timer callbacks (`System.Timers.Timer`) and SimConnect event handlers run on background threads. Any property update that feeds a WPF binding must be marshalled to the UI thread via `Dispatcher.Invoke()`. Missing this causes intermittent crashes or stale UI.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing any code
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- Update this file if new patterns emerge

**For Humans:**

- Keep this file lean and focused on agent needs
- Update when technology stack changes
- Review quarterly for outdated rules
- Remove rules that become obvious over time

Last Updated: 2026-03-11

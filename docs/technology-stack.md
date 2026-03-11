# FSTRaK — Technology Stack

## Overview

| Attribute | Value |
|-----------|-------|
| **Project Type** | Desktop Application (WPF) |
| **Repository Type** | Monolith |
| **Primary Language** | C# (LangVersion: latest) |
| **Framework** | .NET Framework 4.7.2 |
| **Platform Target** | x64 (required — SimConnect native DLL) |
| **Architecture Pattern** | MVVM + State Machine |

## Dependencies

| Category | Technology | Version | Purpose |
|----------|-----------|---------|---------|
| UI Framework | WPF | Built-in (.NET Fx 4.7.2) | Desktop GUI with XAML |
| Database | SQLite | 1.0.119 | Local flight persistence |
| ORM | Entity Framework 6 | 6.5.1 | Data access, automatic migrations |
| Mapping | XAML.MapControl.WPF | 13.4.0 | Interactive maps + tile caching |
| Charting | ScottPlot.WPF | 4.1.74 | Altitude/speed charts |
| Logging | Serilog | 4.2.0 | Structured logging (File + Trace sinks) |
| CSV Parsing | CsvHelper | 33.1.0 | Airport data parsing |
| JSON (legacy) | Newtonsoft.Json | 13.0.3 | VATSIM data deserialization |
| JSON (modern) | System.Text.Json | 9.0.4 | Additional JSON handling |
| SimConnect SDK | Microsoft.FlightSimulator.SimConnect | 11.0.62651.3 | MSFS integration (native x64 DLL) |
| XAML Behaviors | Microsoft.Xaml.Behaviors.Wpf | 1.1.135 | MVVM behavior bindings |
| Installer | Setup.vdproj | Visual Studio Setup | MSI installer project |

## Architecture Pattern

**MVVM (Model-View-ViewModel)** for the presentation layer:
- Views (XAML) bind to ViewModels via `INotifyPropertyChanged`
- `MainWindowViewModel` orchestrates navigation between Live, Logbook, Statistics, and Settings views
- `RelayCommand` pattern for UI actions

**State Machine** for flight lifecycle management:
- State Pattern implementation in `BusinessLogic/FlightManager/State/`
- 10 states governing the complete flight lifecycle
- State transitions driven by SimConnect data (ground speed, altitude, engine RPM, parking brake, etc.)

## Key Architectural Constraints

- **x64-only**: SimConnect native DLL requires x64 platform target
- **Windows-only**: WPF + SimConnect are Windows-exclusive technologies
- **Win32 message pump**: SimConnect messages received via `HwndSource` hook on WPF window handle
- **Single instance**: Mutex ensures only one FSTRaK instance runs at a time

## Notes

- Dual JSON libraries present (`Newtonsoft.Json` + `System.Text.Json`) — consolidation opportunity
- No test project exists in the solution
- ScottPlot v4 series (v5 migration would be non-trivial)
- SQLite interop DLLs manually included for both x86/x64

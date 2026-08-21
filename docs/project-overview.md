# FSTRaK — Project Overview

## What is FSTRaK?

FSTRaK is a flight tracker and logbook application for Microsoft Flight Simulator (MSFS 2020 and 2024). It runs silently in the background, automatically detects when a flight begins, tracks it on a live map, scores the flight quality, and saves completed flights to a local database.

## Key Features

- **Automatic flight tracking** — No manual intervention required. FSTRaK detects flights via SimConnect and tracks them hands-free.
- **Live map** — Real-time aircraft position on an interactive map with multiple tile providers (OpenStreetMap, SkyVector VFR/IFR, MapTiler, Azure Maps, Bing Maps).
- **Flight scoring** — Automatic quality scoring based on landing rate, envelope exceedances (overspeed, stall, flaps, gear), with a 0-110 point scale.
- **VATSIM integration** — Live overlay of online pilots, controllers, FIR/UIR boundaries, and ATIS broadcasts on the tracking map.
- **SimBrief integration** — Fetches your latest SimBrief OFP and overlays the planned route on the live map, with a planned-vs-actual comparison (fuel, block time, distance, payload, pax/cargo) saved with the flight.
- **Flight logbook** — Browse and search completed flights with detailed replay, altitude/speed charts, and scoring breakdowns.
- **Statistics dashboard** — Aggregate flight metrics and performance data.
- **Theming** — Light and dark themes with configurable fonts.
- **MSFS 2020 + 2024 support** — Automatic version detection with version-specific aircraft identification.

## Technical Summary

| Attribute | Value |
|-----------|-------|
| Type | WPF Desktop Application |
| Language | C# (.NET Framework 4.7.2) |
| Platform | Windows x64 only |
| Database | SQLite (local, via Entity Framework 6) |
| MSFS Integration | SimConnect SDK (native x64) |
| Architecture | MVVM + State Machine |
| Mapping | XAML.MapControl.WPF v13.4 |
| Charting | ScottPlot.WPF v4 |
| Logging | Serilog (file + trace) |

## Roadmap

From the project README:
- Enhanced statistics (on-time performance, passengers flown, best/worst landings)
- VATSIM traffic and ATC display improvements

## Repository

- **Solution:** `FSTRaK.sln`
- **Main project:** `FSTRaK/FSTrAk.csproj`
- **Installer:** `Setup/Setup.vdproj`
- **GitHub:** https://github.com/o4oren/FSTRaK

# FSTRaK 3.2.1 Release Notes

## New Features

**TRACON Boundary Polygons**
APP/DEP controllers on the VATSIM overlay now display real TRACON boundary polygons instead of a generic 80km circle. Polygon data is sourced from the [simaware-tracon-project](https://github.com/vatsimnetwork/simaware-tracon-project). When no polygon exists for an airport, the 80km circle fallback is retained.

**Automatic Data File Updates**
`Boundaries.geojson`, `TRACONBoundaries.geojson`, and `VATSpy.dat` are no longer bundled with the installer. On startup, FSTRaK checks for newer releases on GitHub/VATSIM and downloads updated files automatically to `%LOCALAPPDATA%\FSTRaK\Data\`. Previously downloaded files are used as fallback if the download fails.

**SkyVector Maps Fixed**
SkyVector VFR, IFR High, and IFR Low charts are working again. The tile server authentication key rotates with each AIRAC cycle and is now fetched dynamically from the SkyVector API rather than being hardcoded. The key and AIRAC code are refreshed automatically when the current cycle expires.

## Improvements

- Aircraft icon mappings improved with additional type coverage
- Flight path color restored to bold red in light theme

## Bug Fixes

- Fixed blank UI on startup caused by a deadlock in SkyVector tile source initialization
- Fixed crash in Live View when no flight is active (null geometry cast)
- Fixed `System.Buffers` assembly version conflict when loading SkyVector tiles on .NET Framework 4.7.2

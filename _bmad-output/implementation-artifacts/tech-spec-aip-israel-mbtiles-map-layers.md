---
title: 'AIP Israel MBTiles Map Layers'
slug: 'aip-israel-mbtiles-map-layers'
created: '2026-03-12'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['.NET Framework 4.7.2', 'C#', 'WPF', 'XAML.MapControl.WPF 13.4', 'System.Data.SQLite 1.0.119']
files_to_modify:
  - 'FSTRaK/Resources/Data/' (add 4 .mbtiles files)
  - 'FSTRaK/Utils/MBTilesTileSource.cs' (new)
  - 'FSTRaK/Utils/MBTilesMapTileLayer.cs' (new)
  - 'FSTRaK/Resources/MapProvidersDictionary.xaml'
  - 'FSTRaK/FSTrAk.csproj'
code_patterns: ['MapTileLayer subclass', 'TileSource LoadImageAsync override', 'CLR property wiring TileSource', 'exe-relative resource path', 'None CopyToOutputDirectory']
test_patterns: ['manual only']
---

# Tech-Spec: AIP Israel MBTiles Map Layers

**Created:** 2026-03-12

## Overview

### Problem Statement

FSTRaK has no way to display Israeli aviation charts (AIP Israel) as selectable map options. Pilots flying in Israel need access to CVFR (Controlled VFR), LSA (Limited Segregated Airspace), ATS Routes, and Helicopter Routes charts directly within FSTRaK's map view — both live tracking and flight replay.

### Solution

Implement two new classes — `MBTilesTileSource` (reads tiles directly from a local MBTiles SQLite file via `LoadImageAsync` override) and `MBTilesMapTileLayer` (a `MapTileLayer` subclass with a `FilePath` CLR property) — and register 4 AIP Israel chart entries in `MapProvidersDictionary.xaml`. The 4 `.mbtiles` files live in `FSTRaK/Resources/Data/`, are declared in `FSTRaK.csproj` with `CopyToOutputDirectory`, and are accessed at runtime from `{exe_dir}/Resources/Data/` — the same pattern as `airports.csv`.

### Scope

**In Scope:**
- `MBTilesTileSource : TileSource` — SQLite direct read, TMS Y-flip, per-request connection, graceful null return if file missing or tile not found
- `MBTilesMapTileLayer : MapTileLayer` — CLR `FilePath` string property that wires `MBTilesTileSource`; path resolved relative to exe directory
- 4 entries in `MapProvidersDictionary.xaml`: AIP Israel CVFR, AIP Israel LSA, AIP Israel ATS Routes, AIP Israel Helicopter Routes
- Copy 4 `.mbtiles` files from `~/Downloads/layers/` into `FSTRaK/Resources/Data/`
- `<Compile>` entries for the 2 new `.cs` files and `<None CopyToOutputDirectory>` entries for the 4 `.mbtiles` files in `FSTRaK.csproj`
- Zero changes to `SettingsViewModel`, `MapProviderResolver`, `LiveView`, or `LogbookView`

**Out of Scope:**
- OSM base layer hardcoded underneath (future enhancement — documented as TODO)
- `Setup/Setup.vdproj` changes (manual step after dev validation — see Notes)
- Generic user-supplied MBTiles support
- Chart version/update mechanism
- First-run or LocalAppData copying logic

---

## Context for Development

### Codebase Patterns

- **New `.cs` files must be added to `.csproj` manually** — legacy-style project (not SDK-style). New files need `<Compile Include="Utils\Filename.cs" />` alongside existing Utils entries (~line 164).
- **New data files use `<None>` build action** — `airports.csv` is declared as `<None Include="Resources\Data\airports.csv"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`. Use `<None>`, not `<Content>`, for data files.
- **Map tile layer auto-discovery** — `SettingsViewModel` constructor enumerates `MapProvidersDictionary.xaml` and adds any `MapTileLayerBase` or `WmsImageLayer` resource to the dropdown automatically. Adding XAML entries is sufficient — no settings code changes needed.
- **`MapProviderResolver.GetMapProvider()`** — returns any `MapTileLayerBase` subclass by resource key; no changes needed.
- **Runtime path to data files** — use `System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)` then `Path.Combine(..., "Resources", "Data", filename)`. Same pattern as `AirportResolver.LoadAirportsJson()`.
- **`TileSource.LoadImageAsync(int column, int row, int zoomLevel)`** — confirmed virtual override in XAML.MapControl 13.4. When `GetUri` returns `null`, MapControl calls this overload. Returns `Task<ImageSource>`. Parameter order matches `GetUri`: `(int column, int row, int zoomLevel)` — verified against `SkyVectorTileSource`.
- **TMS Y-flip** — MBTiles stores tiles in TMS (Y-axis inverted vs. XYZ/Google). Convert before querying: `tmsRow = (1 << zoomLevel) - 1 - row`.
- **Per-request SQLiteConnection** — open a new connection, query, close per tile request. No shared connection, no lock. Local file overhead is negligible; avoids threading concerns entirely.
- **Existing `MapTileLayer` subclass pattern** — see `SkyVectorMapTileLayer` (sets `TileSource` in constructor) and `MapTilerMapTileLayer` (overrides `UpdateTileLayerAsync`). Our pattern sets `TileSource` in the `FilePath` property setter instead.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `FSTRaK/Utils/SkyVectorMapTileLayer.cs` | Pattern: `MapTileLayer` subclass setting `TileSource` |
| `FSTRaK/Utils/SkyVectorTileSource.cs` | Pattern: `TileSource` subclass overriding `GetUri` |
| `FSTRaK/Models/AirportResolver.cs` | Pattern: exe-relative `Resources/Data/` path resolution |
| `FSTRaK/Resources/MapProvidersDictionary.xaml` | Where new XAML entries are appended |
| `FSTRaK/ViewModels/SettingsViewModel.cs` | Confirms auto-discovery of new providers — no changes needed |
| `FSTRaK/Utils/MapProviderResolver.cs` | Confirms generic handling — no changes needed |
| `FSTRaK/FSTrAk.csproj` | Add `<Compile>` (~line 178) and `<None>` (~line 375) entries |

### Technical Decisions

- **Standalone, not overlay**: MBTiles charts appear as independent options in the existing settings dropdown. No OSM base layer underneath. Future TODO documented.
- **Single class, 4 instances**: `MBTilesTileSource(string filePath)` takes a resolved absolute path. All 4 charts use the same class instantiated 4 times.
- **Per-request SQLiteConnection**: Open, query, close for each tile request. No shared state, no threading issues, negligible overhead for local SQLite.
- **No new NuGet dependencies**: `System.Data.SQLite` (v1.0.119) is already referenced in the project.
- **`x:Shared="false"` on all XAML entries**: Required so each map view gets its own `MBTilesMapTileLayer` instance — same as all existing providers.
- **Zoom range `0–20`**: Wide range ensures tile requests at all zoom levels. At zoom levels outside the MBTiles file's actual tile range, null is returned (blank tiles). The charts only contain tiles at specific zoom ranges (e.g., CVFR: 7–12) so the map will be blank when zoomed out beyond coverage.
- **`FilePath` as CLR string property**: Set by XAML attribute after construction. Setter resolves full path immediately and assigns `TileSource`. No DependencyProperty needed since no binding or animation required.

---

## Implementation Plan

### Tasks

- [x] **Task 1: Copy MBTiles files into project**
  - File: `FSTRaK/Resources/Data/`
  - Action: Copy all 4 files from `~/Downloads/layers/` into `FSTRaK/Resources/Data/`:
    - `CVFR.mbtiles`
    - `LSA.mbtiles`
    - `ATS Routes.mbtiles`
    - `Helicopter Routes.mbtiles`

- [x] **Task 2: Create `MBTilesTileSource.cs`**
  - File: `FSTRaK/Utils/MBTilesTileSource.cs`
  - Action: Create new class. Key behaviors:
    - `GetUri` returns `null` so MapControl falls through to `LoadImageAsync`
    - `LoadImageAsync` queries tiles table via per-request SQLiteConnection; returns decoded image or null
    - TMS Y-flip applied before querying: `tmsRow = (1 << zoomLevel) - 1 - row`
    - `BitmapDecoder.Create` for thread-safe image decoding; `SQLiteConnectionStringBuilder` for safe connection strings
    - All `ImageSource` objects are `Freeze()`d for cross-thread WPF use
  - See `FSTRaK/Utils/MBTilesTileSource.cs` for full implementation.

- [x] **Task 3: Create `MBTilesMapTileLayer.cs`**
  - File: `FSTRaK/Utils/MBTilesMapTileLayer.cs`
  - Action: Create new class with the following implementation:
    ```csharp
    using System.IO;
    using System.Reflection;
    using MapControl;

    namespace FSTRaK.Utils
    {
        public class MBTilesMapTileLayer : MapTileLayer
        {
            private string _filePath;

            public string FilePath
            {
                get => _filePath;
                set
                {
                    _filePath = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        if (exeDir == null) return;
                        var resolved = Path.Combine(exeDir, "Resources", "Data", value);
                        TileSource = new MBTilesTileSource(resolved);
                    }
                    else
                    {
                        TileSource = null;
                    }
                }
            }
        }
    }
    ```
  - Notes: Path resolution uses `Assembly.GetExecutingAssembly().Location` — same pattern as `AirportResolver`. For Debug builds this resolves to `bin/x64/Debug/Resources/Data/`. For Release builds: `bin/x64/Release/Resources/Data/`. Accepts just a filename (e.g. `"CVFR.mbtiles"`) — full-path values also work since `MBTilesTileSource` checks `File.Exists`.

- [x] **Task 4: Register 4 providers in `MapProvidersDictionary.xaml`**
  - File: `FSTRaK/Resources/MapProvidersDictionary.xaml`
  - Action: Append the following 4 entries before the closing `</ResourceDictionary>` tag. The `xmlns:utils` namespace is already declared.
    ```xml
    <utils:MBTilesMapTileLayer
        x:Key="AIP Israel CVFR"
        FilePath="CVFR.mbtiles"
        SourceName="AIPIsraelCVFR"
        Description="© AIP Israel"
        MinZoomLevel="0" MaxZoomLevel="20"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
    <utils:MBTilesMapTileLayer
        x:Key="AIP Israel LSA"
        FilePath="LSA.mbtiles"
        SourceName="AIPIsraelLSA"
        Description="© AIP Israel"
        MinZoomLevel="0" MaxZoomLevel="20"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
    <utils:MBTilesMapTileLayer
        x:Key="AIP Israel ATS Routes"
        FilePath="ATS Routes.mbtiles"
        SourceName="AIPIsraelATSRoutes"
        Description="© AIP Israel"
        MinZoomLevel="0" MaxZoomLevel="20"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
    <utils:MBTilesMapTileLayer
        x:Key="AIP Israel Helicopter Routes"
        FilePath="Helicopter Routes.mbtiles"
        SourceName="AIPIsraelHelicopterRoutes"
        Description="© AIP Israel"
        MinZoomLevel="0" MaxZoomLevel="20"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
    ```

- [x] **Task 5: Update `FSTRaK.csproj`**
  - File: `FSTRaK/FSTrAk.csproj`
  - Action A — Add 2 `<Compile>` entries in the Utils block (after line ~178, alongside `SkyVectorTileSource.cs`):
    ```xml
    <Compile Include="Utils\MBTilesTileSource.cs" />
    <Compile Include="Utils\MBTilesMapTileLayer.cs" />
    ```
  - Action B — Add 4 `<None>` entries in the data files block (alongside `airports.csv` at ~line 375):
    ```xml
    <None Include="Resources\Data\CVFR.mbtiles">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="Resources\Data\LSA.mbtiles">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="Resources\Data\ATS Routes.mbtiles">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="Resources\Data\Helicopter Routes.mbtiles">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    ```

### Acceptance Criteria

- [x] **AC1 — Providers appear in settings dropdown**
  - Given the app is built and running, and the Settings view is opened
  - When the map provider dropdown is expanded
  - Then all four options are present: "AIP Israel CVFR", "AIP Israel LSA", "AIP Israel ATS Routes", "AIP Israel Helicopter Routes"

- [x] **AC2 — Chart tiles render correctly**
  - Given an AIP Israel provider is selected in Settings and the app is restarted or the map refreshed
  - When the Live map or Logbook replay map is panned over Israel at zoom level 8–14
  - Then aviation chart tiles render at the correct positions (no vertical mirroring or offset), the map does not crash, and no exceptions appear in the Visual Studio Output window

- [x] **AC3 — Graceful degradation when file missing**
  - Given an AIP Israel provider is selected but the corresponding `.mbtiles` file is absent from `{exe_dir}/Resources/Data/`
  - When the map view is opened and tiles are requested
  - Then the map displays empty tiles (blank/transparent) without throwing an exception or crashing the app

- [x] **AC4 — No regressions on existing providers**
  - Given any pre-existing map provider (OpenStreetMap, SkyVector VFR, MapTiler, Azure Maps, etc.) is selected
  - When the map view is used normally
  - Then it behaves identically to before this change (tiles load, no errors)

- [x] **AC5 — Settings persist across restart**
  - Given "AIP Israel CVFR" is selected as the map provider and settings are saved (or the app exits normally)
  - When the app is restarted
  - Then the map loads with "AIP Israel CVFR" as the active provider


---

## Additional Context

### Dependencies

- `System.Data.SQLite` v1.0.119 — already in project. No new NuGet packages required.
- `XAML.MapControl.WPF` v13.4 — `TileSource.LoadImageAsync(int column, int row, int zoomLevel)` confirmed as virtual override point; returns `Task<ImageSource>`; called when `GetUri` returns `null`. Parameter order is `(column, row, zoomLevel)` matching MapControl 13.4 convention.
- 4 `.mbtiles` files — available at `~/Downloads/layers/`; committed to `FSTRaK/Resources/Data/` as part of Task 1.

### Testing Strategy

Manual testing only (no automated test infrastructure in this project).

1. Build in `Debug|x64`. Confirm build succeeds with no errors.
2. Verify all 4 new providers appear in Settings → Map Provider dropdown (AC1).
3. Select "AIP Israel CVFR", navigate to Live map, pan over Israel (~lat 32, lon 35) at zoom 9–12. Verify tiles render correctly, no mirroring (AC2).
4. Repeat step 3 for LSA, ATS Routes, Helicopter Routes.
5. Delete or rename `CVFR.mbtiles` from output `Resources/Data/`, select "AIP Israel CVFR" in settings, verify empty map with no crash (AC3).
6. Switch back to OpenStreetMap, SkyVector VFR, and MapTiler — verify all still work normally (AC4).
7. Select "AIP Israel LSA", close and reopen the app, verify it loads with LSA selected (AC5).


### Notes

- **Future TODO — OSM overlay**: A more advanced UX would hardcode OSM as a silent base layer beneath the selected MBTiles chart in `LiveView.xaml` and `LogbookView.xaml`, providing geographic context outside Israel's chart coverage. Deferred — current implementation keeps charts standalone.
- **TMS Y-flip is mandatory** — without `tmsRow = (1 << zoomLevel) - 1 - row`, tiles appear in wrong vertical positions. This is a requirement of the MBTiles spec; confirmed critical in brainstorming.
- **File names with spaces** (`ATS Routes.mbtiles`, `Helicopter Routes.mbtiles`) — `Path.Combine` handles spaces correctly; no special quoting or escaping needed.
- **`Read Only=True` in connection string** — prevents SQLite from creating `-wal` and `-shm` journal files alongside the `.mbtiles` files in `Resources/Data/`. Important for keeping the output directory clean.
- **`frame.Freeze()` is required** — WPF requires `ImageSource` objects to be frozen before use across threads. MapControl's tile scheduler operates on background threads; forgetting `Freeze()` causes an `InvalidOperationException` at runtime. `BitmapDecoder.Create` is used instead of `BitmapImage` for thread safety on ThreadPool threads.
- **Setup.vdproj update (manual, post-dev)**: After validating in a dev build, add 4 file entries to `Setup/Setup.vdproj` via Visual Studio's Setup project UI (Add → Project Output / File). Target the `Resources\Data` application folder. Same structure as existing `airports.csv` entry. `.vdproj` requires unique GUIDs per entry — best generated by VS rather than manually.

## Review Notes

- Adversarial review completed (auto-fix)
- Findings: 5 total, 5 fixed, 0 skipped
- Resolution approach: auto-fix
- F1 (Critical): Replaced `BitmapImage` with `BitmapDecoder.Create` — safe on ThreadPool threads, no Dispatcher required
- F2 (Important): Wrapped `MemoryStream` in `using` block — eliminates per-tile memory leak
- F3 (Important): Replaced string-interpolated connection string with `SQLiteConnectionStringBuilder` — safe for paths containing semicolons
- F4 (Important): Added null guard on `exeDir` before `Path.Combine` in `MBTilesMapTileLayer`
- F5 (Low): `FilePath` setter now clears `TileSource` when set to null/empty

## Post-Implementation Bugfix — Blank Map

**Symptom:** Selecting any AIP Israel map provider showed a blank white map at default zoom levels.

**Root cause:** `MinZoomLevel="7"` was too restrictive. The map's default zoom level when viewing a large area (e.g., all of Israel) is below 7, so MapControl never requested tiles. Tiles only appeared when zoomed in close enough to reach zoom level 7+.

**Misdiagnosis (reverted):** Initially diagnosed as `LoadImageAsync` not being called by MapControl 13.4 when `GetUri` returns null. An `HttpListener`-based local tile server (`MBTilesLocalServer.cs`) was built as a workaround. This was unnecessary — `LoadImageAsync` works correctly in MapControl 13.4.

**Fix:** Changed `MinZoomLevel="0" MaxZoomLevel="20"` on all 4 XAML entries. Reverted to the original direct `LoadImageAsync` approach. Removed `MBTilesLocalServer.cs`.

**Lesson learned:**
- `GetUri` and `LoadImageAsync` parameter order in MapControl 13.4 is `(int column, int row, int zoomLevel)` — NOT `(int zoomLevel, int column, int row)`. Since all params are `int`, the compiler won't catch a mismatch. Verified against `SkyVectorTileSource`.
- `MinZoomLevel` on `MapTileLayer` controls which zoom levels trigger tile requests. Setting it too high prevents tiles from appearing at default/overview zoom levels.

## Abandoned — Low-Zoom Placeholder Tiles

Attempted to show semi-transparent placeholder tiles at zoom levels below the chart's `minzoom` to indicate where chart coverage exists. Two approaches were tried:

1. **Metadata bounds overlap** — checked if tile's geographic extent intersected the MBTiles `bounds` field. At low zoom levels, tiles cover huge geographic areas (a zoom-4 tile spans ~45° of longitude), so nearly every visible tile overlapped — result was the entire map turning blue.

2. **Parent tile index** — queried actual tile coordinates at `minzoom` and precomputed parent tiles via bit-shifting. Mathematically correct, but at zoom 0–3 a single tile covers continents so the result was still too much blue. Limiting to 3 levels below `minzoom` still produced unsatisfactory results.

**Decision:** Removed placeholder tiles entirely. The chart maps are intended for use zoomed in over Israel; blank tiles at low zoom are acceptable.

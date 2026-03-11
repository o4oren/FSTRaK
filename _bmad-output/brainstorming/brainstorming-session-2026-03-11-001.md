---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Integrating AIP Israel MBTiles as selectable map layers in FSTRaK'
session_goals: 'Explore all approaches to add 4 local MBTiles files (CVFR, LSA, ATS Routes, Helicopter Routes) as selectable map options in FSTRaK map settings'
selected_approach: 'ai-recommended'
techniques_used: ['Question Storming', 'SCAMPER Method', 'Constraint Mapping']
ideas_generated: [16]
session_active: false
workflow_completed: true
---

# Brainstorming Session Results

**Facilitator:** Oren
**Date:** 2026-03-11

---

## Session Overview

**Topic:** Integrating AIP Israel MBTiles as selectable map layers in FSTRaK
**Goals:** Explore all approaches to add 4 local MBTiles files (CVFR, LSA, ATS Routes, Helicopter Routes) as selectable map options in FSTRaK map settings

### Context

FSTRaK uses `XAML.MapControl.WPF` v13.4. Map providers are declared in `Resources/MapProvidersDictionary.xaml` as `MapTileLayer` or custom subclasses (`SkyVectorMapTileLayer`, `MapTilerMapTileLayer`, `AzureMapsMapTileLayer`). The selected provider key is stored in `Properties.Settings.Default.MapTileProvider` and resolved via `MapProviderResolver`.

**The 4 MBTiles files:**
- `CVFR.mbtiles` — Controlled VFR airspace chart
- `LSA.mbtiles` — Limited Segregated Airspace chart
- `ATS Routes.mbtiles` — Air Traffic Service routes chart
- `Helicopter Routes.mbtiles` — Helicopter routes chart

### Key Design Decisions (from Question Storming)

| Decision | Answer |
|---|---|
| Overlay or standalone? | Overlay on OSM preferred; opaque-over-base acceptable |
| File location | `%LOCALAPPDATA%\FSTRaK` |
| Versioning/updates | Via new app releases only |
| Transparency | Don't care — opaque coverage is fine |
| Scope | Personal simulation build |

---

## Technique Selection

**Approach:** AI-Recommended
**Sequence:** Question Storming → SCAMPER Method → Constraint Mapping

---

## All Ideas Generated

### Design Decisions

**[Design #1]**: Overlay-First Architecture
_Concept_: MBTiles layers render on top of a base map (OpenStreetMap). Outside Israel the overlay is transparent/empty, so the base map shows through. User experience: pick a base map + toggle one or more AIP Israel layers.
_Novelty_: This is how aviation charts work in real tools (ForeFlight, SkyVector) — sectional on top of satellite/street.

**[Design #2]**: Managed Deployment Model
_Concept_: MBTiles files ship with the app and live in `%LOCALAPPDATA%\FSTRaK`. No user configuration, no path settings UI, no versioning logic. Files are updated only via new app releases, exactly like `airports.csv` today.
_Novelty_: Treats MBTiles as static assets, not user data — dead simple.

---

### Core Technical Implementation

**[Impl #1 — CONFIRMED VIABLE]**: MBTilesTileSource — SQLite Direct
_Concept_: Subclass `TileSource`, override `GetUri → null`, override `LoadImageAsync(int zoomLevel, int column, int row)` to open the MBTiles SQLite file and return the raw tile blob. Wrap in `MBTilesMapTileLayer : MapTileLayer` with a `FilePath` property. No HTTP, no infrastructure.
_Novelty_: Confirmed by XAML-Map-Control source: `TileSource.LoadImageAsync(zoom, col, row)` is a virtual override point. When `GetUri` returns null, MapControl calls `LoadImageAsync` instead.

**[Impl #2]**: Local HTTP Tile Server _(rejected — too heavy)_
_Concept_: Spin up a tiny embedded HTTP listener on `localhost:PORT` at startup. Serves tile requests by reading from MBTiles SQLite. The `TileSource` URI template becomes `http://localhost:PORT/{z}/{x}/{y}.png`.
_Novelty_: Works with vanilla `MapTileLayer` — zero MapControl customization. Rejected in favour of Impl #1 due to infrastructure overhead.

**[Impl #5]**: Reuse Existing SQLite Infrastructure
_Concept_: The project already has `XAML.MapControl.SQLiteCache` and EF6/SQLite as dependencies. MBTiles is standard SQLite with a `tiles` table. No new SQLite library needed.
_Novelty_: Zero new dependencies.

**[Impl #6]**: One `MBTilesTileSource`, N Instances
_Concept_: `MBTilesTileSource` takes a `string FilePath` constructor argument. All 4 MBTiles files share the same class, instantiated 4 times in `MapProvidersDictionary.xaml` with different paths to `%LOCALAPPDATA%\FSTRaK\*.mbtiles`.
_Novelty_: Zero code duplication — one class handles all charts.

**[Impl #7]**: Zoom-Aware Tile Serving
_Concept_: Set `MinZoomLevel`/`MaxZoomLevel` on each `MBTilesMapTileLayer` to match the zoom range actually present in each MBTiles file. At unsupported zoom levels MapControl shows nothing — no errors, no blank tile requests.
_Novelty_: Same pattern already used by SkyVector layers in the project.

**[Impl #8]**: Lazy File Open with Graceful Degradation
_Concept_: Don't open the SQLite connection in the constructor. Open on first tile request, cache the connection. If the file doesn't exist at `%LOCALAPPDATA%\FSTRaK`, silently return null tiles — the map shows empty rather than crashing.
_Novelty_: Safe for first run before files are deployed.

---

### Architecture & Integration

**[Impl #3]**: MBTiles + OSM Overlay Stack in XAML
_Concept_: Stack two `MapTileLayer`s in XAML — OSM as the base layer (always present), `MBTilesMapTileLayer` on top. The settings picker selects which MBTiles overlay is active, independently from the base map.
_Novelty_: WPF panels are naturally layered — this requires no MapControl changes, just XAML structure.

**[Impl #11]**: No Settings UI Change Required
_Concept_: The 4 MBTiles entries in `MapProvidersDictionary.xaml` with `x:Key` names appear automatically in the existing settings dropdown. No new settings UI code at all.
_Novelty_: The existing settings infrastructure handles discovery — adding XAML resource entries is sufficient.

**[Impl #12]**: No `MapProviderResolver` Change Required
_Concept_: `MapProviderResolver.GetMapProvider()` already handles any `MapTileLayerBase` subclass generically. As long as `MBTilesMapTileLayer` extends `MapTileLayer`, zero changes to the resolver.
_Novelty_: Zero risk of breaking existing map providers.

**[Impl #13]**: Overlay as Primary, OSM Always Underneath
_Concept_: Flip the mental model — the MBTiles chart IS the selected map, and OSM is always silently rendered underneath as a context layer (hardcoded in XAML, not user-selectable). Selecting "AIP Israel CVFR" automatically gives OSM + chart.
_Novelty_: Simpler UX — user makes one selection, not two ("base map" + "overlay").

**[Impl #14]**: Embedded Resource Extraction on First Run
_Concept_: Bundle MBTiles files as embedded resources. On first run, extract to `%LOCALAPPDATA%\FSTRaK` — same pattern used for `airports.csv`. Installer stays lean.
_Novelty_: Files land in the right place automatically without user action; follows existing project convention.

---

### UX / Settings Design

**[Impl #4]**: Combined "Base + Overlay" Picker Entry
_Concept_: A single settings entry like "OSM + AIP CVFR" encodes both base map and overlay. `MapProviderResolver` returns a composite containing two layers.
_Novelty_: One dropdown, no "overlay" concept exposed to user — simpler at cost of more picker entries.

---

### Future Possibilities

**[Impl #9]**: Generic MBTiles Support for Any Region
_Concept_: Since `MBTilesTileSource` takes any file path, the same implementation later supports user-supplied MBTiles from any source (other AIP regions, OpenMapTiles exports). Israel files are just the first use.
_Novelty_: Future-proof with zero extra work now.

**[Impl #10]**: MBTiles as Flight Replay Overlay
_Concept_: The flight replay map view (Logbook) shares `MapProvidersDictionary.xaml`. MBTiles overlays automatically work in replay too — historical Israeli flights plotted on AIP charts.
_Novelty_: Free benefit with no additional code.

---

### Critical Technical Constraint Discovered

**TMS Y-Coordinate Flip (REAL constraint):**
MBTiles stores tiles in TMS coordinate system (Y-axis flipped vs. standard XYZ/Google/MapControl). The SQLite query **must** convert: `tms_row = (1 << zoomLevel) - 1 - row`. Without this, tiles appear mirrored vertically or in wrong positions.

---

## Idea Organization and Prioritization

### Themes

**Theme 1: Core Engine** _(must-have)_
Impl #1, #5, #6, #8 + TMS Y-flip constraint
→ `MBTilesTileSource` with SQLite direct read, one class for all 4 files, lazy open, Y-flip.

**Theme 2: Zero-Friction Integration** _(must-have)_
Impl #11, #12
→ No changes to Settings UI or MapProviderResolver. Just add XAML entries.

**Theme 3: UX Design Choice** _(decision needed)_
Design #1 + Impl #3 vs. Impl #4 vs. Impl #13
→ How overlays are presented to the user. Recommendation: Impl #13 (MBTiles as standalone option, OSM silently underneath).

**Theme 4: Deployment** _(must-have)_
Design #2 + Impl #14
→ Files in `%LOCALAPPDATA%\FSTRaK`, extracted from embedded resources on first run.

**Theme 5: Polish** _(nice-to-have)_
Impl #7 (zoom limits), #9 (future generic use), #10 (replay benefit)

---

### Prioritization

**Top Priority — Recommended Implementation:**
1. Impl #1 — `MBTilesTileSource : TileSource` with `LoadImageAsync` override + TMS Y-flip
2. Impl #6 — Single class, 4 `MapProvidersDictionary.xaml` entries with `FilePath`
3. Impl #13 — OSM hardcoded as silent base; MBTiles as the "selected map"
4. Impl #14 — Embedded resource extraction to `%LOCALAPPDATA%\FSTRaK` on first run
5. Impl #8 — Lazy open + graceful degradation if file missing

**Quick Wins (free once core is done):**
- Impl #11 — Settings dropdown works automatically
- Impl #12 — MapProviderResolver works automatically
- Impl #10 — Replay map works automatically

**Skip for now:**
- Impl #2 — Local HTTP server (overkill)
- Impl #4 — Combined picker (adds complexity without benefit)
- Impl #9 — Generic support (future release)

---

## Action Plan

### Recommended Implementation Sequence

**Step 1: Inspect one MBTiles file**
Open `CVFR.mbtiles` in a SQLite browser (e.g., DB Browser for SQLite). Confirm:
- Table schema: `tiles (zoom_level, tile_column, tile_row, tile_data)`
- Tile format: PNG or JPG
- Zoom levels present
- Confirm TMS Y-flip is needed (standard for MBTiles spec)

**Step 2: Create `MBTilesTileSource.cs`**
```csharp
public class MBTilesTileSource : TileSource
{
    private readonly string _filePath;
    private SQLiteConnection _connection;

    public MBTilesTileSource(string filePath) { _filePath = filePath; }

    public override Uri GetUri(int x, int y, int zoomLevel) => null;

    public override async Task<ImageSource> LoadImageAsync(int x, int y, int zoomLevel)
    {
        // Open connection lazily
        // TMS Y-flip: tmsY = (1 << zoomLevel) - 1 - y
        // Query: SELECT tile_data FROM tiles WHERE zoom_level=? AND tile_column=? AND tile_row=?
        // Return BitmapImage from byte[]
    }
}
```

**Step 3: Create `MBTilesMapTileLayer.cs`**
```csharp
public class MBTilesMapTileLayer : MapTileLayer
{
    public static readonly DependencyProperty FilePathProperty = ...;

    public string FilePath { get; set; }

    protected override void OnPropertyChanged(...) {
        // Wire up MBTilesTileSource when FilePath is set
        TileSource = new MBTilesTileSource(ResolvePath(FilePath));
    }

    private string ResolvePath(string path) =>
        path.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(...));
}
```

**Step 4: Add entries to `MapProvidersDictionary.xaml`**
```xml
<utils:MBTilesMapTileLayer
    x:Key="AIP Israel CVFR"
    FilePath="%LOCALAPPDATA%\FSTRaK\CVFR.mbtiles"
    SourceName="AIPIsraelCVFR"
    Description="© AIP Israel"
    MinZoomLevel="7" MaxZoomLevel="14"
    UpdateWhileViewportChanging="true"
    x:Shared="false"/>
<!-- Repeat for LSA, ATS Routes, Helicopter Routes -->
```

**Step 5: Handle OSM as silent base layer**
In the map XAML views (LiveView, LogbookView), add OSM as a permanent base layer beneath the `MapProviderResolver`-selected layer.

**Step 6: Deploy MBTiles files**
Add files as embedded resources. On app startup (or first-run check), extract to `%LOCALAPPDATA%\FSTRaK\` if not present.

---

## Session Summary

**Total ideas generated:** 16 across 3 techniques
**Key breakthrough:** `TileSource.LoadImageAsync(zoom, col, row)` is overridable — confirmed via XAML-Map-Control source. Direct SQLite read is fully viable with zero HTTP infrastructure.
**Critical technical insight:** MBTiles TMS Y-coordinate flip (`tmsRow = (1 << zoom) - 1 - row`) is mandatory for correct tile positioning.
**Architecture verdict:** Minimal-change implementation — 2 new classes, 4 XAML entries, zero changes to existing Settings UI, MapProviderResolver, or any other existing code.

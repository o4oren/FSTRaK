---
title: 'Map Layer Separation — Base, OpenAIP Overlay, Chart Overlay'
slug: 'map-layer-separation'
created: '2026-03-28'
status: 'Implementation Complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['C# .NET Framework 4.7.2', 'WPF', 'XAML.MapControl.WPF v13.4', 'Properties.Settings (WPF user settings)']
files_to_modify:
  - 'FSTRaK/Properties/Settings.settings'
  - 'FSTRaK/Properties/Settings.Designer.cs'
  - 'FSTRaK/Resources/MapProvidersDictionary.xaml'
  - 'FSTRaK/Utils/OpenAipMapTileLayer.cs (NEW)'
  - 'FSTRaK/Utils/MapLayerHelper.cs (NEW)'
  - 'FSTRaK/Utils/MapProviderResolver.cs'
  - 'FSTRaK/ViewModels/SettingsViewModel.cs'
  - 'FSTRaK/Views/SettingsView.xaml'
  - 'FSTRaK/Views/LiveView.xaml.cs'
  - 'FSTRaK/Views/FlightDetailsView.xaml.cs'
  - 'FSTRaK/ViewModels/LiveViewViewModel.cs'
  - 'FSTRaK/ViewModels/FlightDetailsViewModel.cs'
  - 'FSTRaK/Views/MainWindow.xaml.cs'
code_patterns:
  - 'API-key tile layer: subclass MapTileLayer, static ApiKey field, override UpdateTileLayerAsync to Replace("{ApiKey}", ApiKey)'
  - 'IOverlayMapTileLayer marker interface used to classify chart overlays in SettingsViewModel'
  - 'Settings.Default.PropertyChanged drives map refresh via NotifyMapProviderChanged() on VM'
  - 'MapLayer property sets base; Children.Insert adds overlays above base index'
test_patterns: ['No automated tests present in project — manual verification only']
---

# Tech-Spec: Map Layer Separation — Base, OpenAIP Overlay, Chart Overlay

**Created:** 2026-03-28

## Overview

### Problem Statement

All map providers (base maps and aviation chart overlays) are in a single flat dropdown. Users cannot combine a base map with aviation chart overlays (FAA, DFS, Israel AIP) or OpenAIP simultaneously. There is no way to layer maps.

### Solution

Split the map selection UI into three independent controls: a base layer dropdown (non-chart maps only), a chart overlay dropdown (FAA/DFS/Israel AIP + None), and an OpenAIP checkbox with API key field. The map renders them stacked: base → OpenAIP → chart overlay.

### Scope

**In Scope:**
- New `OpenAipMapTileLayer` class with static `ApiKey` property (mirrors `AzureMapsMapTileLayer`/`MapTilerMapTileLayer` pattern)
- Uncomment and wire up OpenAIP entry in `MapProvidersDictionary.xaml`
- Split map provider lists: base dropdown excludes chart-overlay entries; chart overlay dropdown includes only `IOverlayMapTileLayer` entries + "None"
- `SettingsViewModel`: add `SelectedChartOverlayProvider`, `IsOpenAipEnabled`, `OpenAipApiKey`, `IsShowOpenAipApiKeyField`, `ChartOverlayProviders` collection
- New settings keys: `ChartOverlayProvider` (string, default "None"), `IsOpenAipEnabled` (bool, default false), `OpenAipApiKey` (string, default "")
- `SettingsView.xaml`: add "Map Overlays" section with chart overlay dropdown, OpenAIP checkbox, OpenAIP API key field
- Extract shared `UpdateMapLayers` logic to `MapLayerHelper` static class
- `UpdateMapLayers` builds layer stack: `[base] → [OpenAIP if enabled] → [chart overlay if not None]`
- Both views track `_currentOpenAipLayer` + `_currentChartLayer` instead of single `_currentOverlayLayer`
- `OnSettingsPropertyChanged` in both views handles `MapTileProvider`, `ChartOverlayProvider`, `IsOpenAipEnabled`
- `MapProviderResolver`: add `GetChartOverlayProvider()` and `GetOpenAipLayer()` helpers
- `MapAttributionText` updated to reflect combined layer attribution
- OpenAIP API key initialized at startup in `MainWindow.xaml.cs`

**Out of Scope:**
- No new map sources beyond OpenAIP
- No changes to map provider tile URLs or zoom levels
- No grouping/combining of Israel AIP layers

## Context for Development

### Codebase Patterns

- **API-key tile layer pattern**: subclass `MapTileLayer`, declare `public static string ApiKey`, override `UpdateTileLayerAsync(bool tileSourceChanged)` — check `TileSource.UriTemplate.Contains("{ApiKey}")`, replace it, then call `base.UpdateTileLayerAsync(tileSourceChanged)`. See `AzureMapsMapTileLayer.cs` (exact template to follow for `OpenAipMapTileLayer`).
- **`IOverlayMapTileLayer`** is an empty marker interface implemented by `OverlayMapTileLayer` (FAA/DFS) and `MBTilesMapTileLayer` (Israel AIP). Used by `SettingsViewModel` to classify chart overlays vs base maps.
- **Old `UpdateMapLayers()` logic to REMOVE**: the `if (provider is IOverlayMapTileLayer)` branch (which set OSM as base and inserted overlay above it) is removed entirely. The new system always uses the base dropdown selection unconditionally.
- **`UpdateMapLayers()` extraction**: duplicated in `LiveView.xaml.cs:89` and `FlightDetailsView.xaml.cs:88`. Extract to `MapLayerHelper` static class.
- **View overlay field tracking**: replace single `_currentOverlayLayer` with `_currentOpenAipLayer` + `_currentChartLayer` in both view code-behinds.
- **Settings change flow**: `Properties.Settings.Default.PropertyChanged` → `OnSettingsPropertyChanged` in view code-behind → calls `vm.NotifyMapProviderChanged()` → VM fires `PropertyChanged("MapProvider")` → view calls `UpdateMapLayers()`. Must handle: `"MapTileProvider"`, `"ChartOverlayProvider"`, `"IsOpenAipEnabled"`.
- **SettingsViewModel provider list construction**: iterates `MapProvidersDictionary.xaml` `ResourceDictionary`. Base list = entries where value is `MapTileLayerBase` AND NOT `IOverlayMapTileLayer`. Chart overlay list = entries where value IS `IOverlayMapTileLayer`, sorted, with `"None"` prepended.
- **`MapAttributionText`** in both VMs: currently appends "Base map: OSM" for overlay layers. Replace with combined attribution from all active layers.
- **`MainWindow.xaml.cs:76-79`**: API keys initialized at startup. Add `OpenAipMapTileLayer.ApiKey = Properties.Settings.Default.OpenAipApiKey` alongside existing Azure/MapTiler inits.
- **`SettingsViewModel.SettingsView_OnLoaded()`**: add loads for `SelectedChartOverlayProvider`, `IsOpenAipEnabled`, `OpenAipApiKey`.
- **Layer insertion order in MapControl**: `map.MapLayer = baseLayer` sets base. Then: get `baseIndex = map.Children.IndexOf(baseLayer)`. Insert OpenAIP at `baseIndex + 1`, chart overlay at `baseIndex + 2` (or append). On tear-down: `map.Children.Remove(_currentOpenAipLayer)` and `map.Children.Remove(_currentChartLayer)` before rebuild.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `FSTRaK/Resources/MapProvidersDictionary.xaml` | All tile layer resource definitions |
| `FSTRaK/Utils/AzureMapsMapTileLayer.cs` | Exact pattern to follow for `OpenAipMapTileLayer` |
| `FSTRaK/Utils/IOverlayMapTileLayer.cs` | Marker interface for chart overlay classification |
| `FSTRaK/Utils/OverlayMapTileLayer.cs` | Chart overlay type (FAA/DFS) |
| `FSTRaK/Utils/MBTilesMapTileLayer.cs` | Chart overlay type (Israel AIP) |
| `FSTRaK/Utils/MapProviderResolver.cs` | Static resolver — add two new methods |
| `FSTRaK/ViewModels/SettingsViewModel.cs` | Settings UI logic — add 3 new settings, split provider lists |
| `FSTRaK/Views/SettingsView.xaml` | Settings UI layout — add Map Overlays section |
| `FSTRaK/Views/LiveView.xaml.cs` | Map layer management — replace UpdateMapLayers with helper call |
| `FSTRaK/Views/FlightDetailsView.xaml.cs` | Map layer management — replace UpdateMapLayers with helper call |
| `FSTRaK/ViewModels/LiveViewViewModel.cs` | Exposes MapProvider, MapAttributionText, NotifyMapProviderChanged |
| `FSTRaK/ViewModels/FlightDetailsViewModel.cs` | Exposes MapProvider, MapAttributionText, NotifyMapProviderChanged |
| `FSTRaK/Properties/Settings.settings` | Persisted settings definitions — add 3 new entries |
| `FSTRaK/Properties/Settings.Designer.cs` | Auto-generated typed accessors — add 3 new properties |
| `FSTRaK/Views/MainWindow.xaml.cs` | API key init at startup (lines 76-79) |

### Technical Decisions

- `OpenAipMapTileLayer` follows the exact same `ApiKey` static field + `UpdateTileLayerAsync` override pattern as `AzureMapsMapTileLayer`. Resource key in XAML: `"OpenAIP"`.
- `MapProviderResolver.GetOpenAipLayer()`: returns `Application.Current.Resources["OpenAIP"] as MapTileLayerBase` if `Settings.Default.IsOpenAipEnabled == true`, else null.
- `MapProviderResolver.GetChartOverlayProvider()`: returns null if `Settings.Default.ChartOverlayProvider == "None"`, else `Application.Current.Resources[key] as MapTileLayerBase`.
- `MapLayerHelper.UpdateMapLayers(MapBase map, ref MapTileLayerBase currentOpenAipLayer, ref MapTileLayerBase currentChartLayer, MapTileLayerBase baseLayer, MapTileLayerBase openAipLayer, MapTileLayerBase chartLayer)` — uses `ref` params to update the view's tracking fields.
- `IsShowOpenAipApiKeyField`: computed property on `SettingsViewModel` returning `IsOpenAipEnabled`.
- `MapAttributionText` new logic: build a list of descriptions from active layers (base, openAIP if enabled, chart overlay if not None), join with " | ".
- `Settings.Designer.cs` must be manually updated (not auto-regenerated) since VS auto-generation would require opening the designer. Add the three new properties following the exact pattern of existing properties in that file.

## Implementation Plan

### Tasks

- [x] **Task 1**: Add three new settings entries
  - File: `FSTRaK/Properties/Settings.settings`
  - Action: Add three `<Setting>` entries:
    - `ChartOverlayProvider` Type=`System.String` Scope=`User` Default=`"None"`
    - `IsOpenAipEnabled` Type=`System.Boolean` Scope=`User` Default=`False`
    - `OpenAipApiKey` Type=`System.String` Scope=`User` Default=`""`

- [x] **Task 2**: Add typed property accessors for new settings
  - File: `FSTRaK/Properties/Settings.Designer.cs`
  - Action: Add three new auto-property getter/setter pairs following the exact pattern of the existing `MapTileProvider` property in that file:
    - `public string ChartOverlayProvider { get => (string)this["ChartOverlayProvider"]; set => this["ChartOverlayProvider"] = value; }`
    - `public bool IsOpenAipEnabled { get => (bool)this["IsOpenAipEnabled"]; set => this["IsOpenAipEnabled"] = value; }`
    - `public string OpenAipApiKey { get => (string)this["OpenAipApiKey"]; set => this["OpenAipApiKey"] = value; }`

- [x] **Task 3**: Create `OpenAipMapTileLayer`
  - File: `FSTRaK/Utils/OpenAipMapTileLayer.cs` (NEW)
  - Action: Create class identical in structure to `AzureMapsMapTileLayer.cs` but named `OpenAipMapTileLayer`, with `internal` visibility. Static field `public static string ApiKey`. Override `UpdateTileLayerAsync` to replace `{ApiKey}` in `TileSource.UriTemplate` before calling base.

- [x] **Task 4**: Update `MapProvidersDictionary.xaml`
  - File: `FSTRaK/Resources/MapProvidersDictionary.xaml`
  - Action 1: Add `xmlns:utils` reference for `OpenAipMapTileLayer` (already present for other utils types — confirm namespace).
  - Action 2: Replace the commented-out OpenAIP block (lines 173-179) with an active `<utils:OpenAipMapTileLayer>` entry:
    ```xml
    <utils:OpenAipMapTileLayer
        x:Key="OpenAIP"
        TileSource="https://api.tiles.openaip.net/api/data/openaip/{z}/{x}/{y}.png?apiKey={ApiKey}"
        SourceName="OpenAIP"
        Description="© [OpenAIP](https://www.openaip.net)"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
    ```
  - Notes: `OpenAipMapTileLayer` does NOT implement `IOverlayMapTileLayer` — it is handled separately from chart overlays. It must NOT appear in either the base or chart overlay dropdown lists in SettingsViewModel.

- [x] **Task 5**: Add `MapProviderResolver` helper methods
  - File: `FSTRaK/Utils/MapProviderResolver.cs`
  - Action: Add two static methods to the existing `MapProviderResolver` class:
    ```csharp
    public static MapTileLayerBase GetChartOverlayProvider()
    {
        var key = Properties.Settings.Default.ChartOverlayProvider;
        if (string.IsNullOrEmpty(key) || key == "None") return null;
        return Application.Current.Resources[key] as MapTileLayerBase;
    }

    public static MapTileLayerBase GetOpenAipLayer()
    {
        if (!Properties.Settings.Default.IsOpenAipEnabled) return null;
        return Application.Current.Resources["OpenAIP"] as MapTileLayerBase;
    }
    ```

- [x] **Task 6**: Create `MapLayerHelper`
  - File: `FSTRaK/Utils/MapLayerHelper.cs` (NEW)
  - Action: Create a static class with one method:
    ```csharp
    public static class MapLayerHelper
    {
        public static void UpdateMapLayers(
            MapBase map,
            ref MapTileLayerBase currentOpenAipLayer,
            ref MapTileLayerBase currentChartLayer)
        {
            // Tear down existing overlay layers
            if (currentOpenAipLayer != null)
            {
                map.Children.Remove(currentOpenAipLayer);
                currentOpenAipLayer = null;
            }
            if (currentChartLayer != null)
            {
                map.Children.Remove(currentChartLayer);
                currentChartLayer = null;
            }

            // Set base layer
            var baseLayer = MapProviderResolver.GetMapProvider();
            if (baseLayer == null) return;
            map.MapLayer = baseLayer;

            // Insert OpenAIP above base
            var openAipLayer = MapProviderResolver.GetOpenAipLayer();
            if (openAipLayer != null)
            {
                var baseIndex = map.Children.IndexOf(baseLayer);
                map.Children.Insert(baseIndex + 1, openAipLayer);
                currentOpenAipLayer = openAipLayer;
            }

            // Insert chart overlay above OpenAIP (or above base if no OpenAIP)
            var chartLayer = MapProviderResolver.GetChartOverlayProvider();
            if (chartLayer != null)
            {
                map.Children.Add(chartLayer);
                currentChartLayer = chartLayer;
            }
        }
    }
    ```
  - Notes: `map.Children.Add` is safe for the chart overlay since it must be the topmost layer. `MapBase` is the base type for `Map` in XAML.MapControl.WPF — confirm the actual type used for `xMap` and `LogbookMap` in the XAML (likely `map:Map`); use that type or the common base.

- [x] **Task 7**: Update `LiveView.xaml.cs`
  - File: `FSTRaK/Views/LiveView.xaml.cs`
  - Action 1: Replace `private MapTileLayerBase _currentOverlayLayer;` with:
    ```csharp
    private MapTileLayerBase _currentOpenAipLayer;
    private MapTileLayerBase _currentChartLayer;
    ```
  - Action 2: In `OnUnLoaded`, replace the `_currentOverlayLayer` removal block with removal of both new fields (using `xMap.Children.Remove`) and set both to null.
  - Action 3: In `OnSettingsPropertyChanged`, extend the condition to:
    ```csharp
    if (e.PropertyName == "MapTileProvider" ||
        e.PropertyName == "ChartOverlayProvider" ||
        e.PropertyName == "IsOpenAipEnabled")
    ```
  - Action 4: Replace the entire body of `UpdateMapLayers()` with:
    ```csharp
    MapLayerHelper.UpdateMapLayers(xMap, ref _currentOpenAipLayer, ref _currentChartLayer);
    var vm = DataContext as LiveViewViewModel;
    vm?.NotifyMapProviderChanged();
    ```
    Notes: Remove the `vm?.MapProvider` null check and old `is IOverlayMapTileLayer` branch entirely.

- [x] **Task 8**: Update `FlightDetailsView.xaml.cs`
  - File: `FSTRaK/Views/FlightDetailsView.xaml.cs`
  - Action: Apply identical changes as Task 7, replacing `xMap` with `LogbookMap` and `LiveViewViewModel` with `FlightDetailsViewModel`.

- [x] **Task 9**: Update `MapAttributionText` in both ViewModels
  - Files: `FSTRaK/ViewModels/LiveViewViewModel.cs`, `FSTRaK/ViewModels/FlightDetailsViewModel.cs`
  - Action: Replace the `MapAttributionText` getter body in both files. New logic:
    ```csharp
    get
    {
        var parts = new List<string>();
        var baseProvider = MapProviderResolver.GetMapProvider();
        if (baseProvider?.Description != null) parts.Add(baseProvider.Description);
        if (Properties.Settings.Default.IsOpenAipEnabled)
            parts.Add("© [OpenAIP](https://www.openaip.net)");
        var chartProvider = MapProviderResolver.GetChartOverlayProvider();
        if (chartProvider?.Description != null) parts.Add(chartProvider.Description);
        return string.Join(" | ", parts);
    }
    ```
  - Action: Also add `OnPropertyChanged(nameof(MapAttributionText))` to `NotifyMapProviderChanged()` (already present — verify it's still called after Task 7/8 changes).

- [x] **Task 10**: Update `SettingsViewModel`
  - File: `FSTRaK/ViewModels/SettingsViewModel.cs`
  - Action 1: Add `public ObservableCollection<string> ChartOverlayProviders { get; set; }` property alongside existing `MapProviders`.
  - Action 2: Add backing field and property for `SelectedChartOverlayProvider` (string). Setter: save to `Properties.Settings.Default.ChartOverlayProvider`.
  - Action 3: Add backing field and property for `IsOpenAipEnabled` (bool). Setter: save to `Properties.Settings.Default.IsOpenAipEnabled`, also set `IsShowOpenAipApiKeyField`.
  - Action 4: Add backing field and property for `OpenAipApiKey` (string). Setter: save to `Properties.Settings.Default.OpenAipApiKey`, set `OpenAipMapTileLayer.ApiKey = value`.
  - Action 5: Add computed property `IsShowOpenAipApiKeyField` (bool, private set) returning `IsOpenAipEnabled`.
  - Action 6: In constructor, split the provider enumeration loop:
    - Base list: `provider.Value is MapTileLayerBase && !(provider.Value is IOverlayMapTileLayer) && !(provider.Value is OpenAipMapTileLayer)` → add key to `layers`
    - Chart overlay list: `provider.Value is IOverlayMapTileLayer` → add key to `chartLayers`
    - After loop: `MapProviders = new ObservableCollection<string>(layers.OrderBy(l => l))`
    - `ChartOverlayProviders = new ObservableCollection<string>(new[] { "None" }.Concat(chartLayers.OrderBy(l => l)))`
  - Action 7: In `SettingsView_OnLoaded()`, add:
    ```csharp
    SelectedChartOverlayProvider = Properties.Settings.Default.ChartOverlayProvider;
    IsOpenAipEnabled = Properties.Settings.Default.IsOpenAipEnabled;
    OpenAipApiKey = Properties.Settings.Default.OpenAipApiKey;
    ```

- [x] **Task 11**: Update `SettingsView.xaml`
  - File: `FSTRaK/Views/SettingsView.xaml`
  - Action: After the existing "Map tiles Provider" StackPanel row (and its associated API key visibility rows), add a new "Map Overlays" labeled section. Add three rows:
    1. A `Label` reading "Map Overlays" styled as a section header (or use `Style="{DynamicResource FSTrAkLabel}"` with `Width="250"` and no control beside it for a divider effect)
    2. Chart overlay row: `Label` "Chart Overlay" + `ComboBox` bound to `ChartOverlayProviders` / `SelectedItem="{Binding SelectedChartOverlayProvider}"`, same style/width as existing map provider ComboBox. ToolTip: "Select an aviation chart overlay (FAA, DFS, Israel AIP) to display above the base map"
    3. OpenAIP row: `Label` "OpenAIP Overlay" + `CheckBox` bound to `IsOpenAipEnabled`. ToolTip: "Enable OpenAIP airspace and aeronautical data overlay"
    4. OpenAIP API key row: `Label` "OpenAIP API Key" + `TextBox` bound to `OpenAipApiKey`, visibility bound to `IsShowOpenAipApiKeyField` (same `BoolToVis` converter pattern as BingApiKey row). ToolTip: "OpenAIP requires a free API key. Register at https://www.openaip.net"

- [x] **Task 12**: Initialize OpenAIP API key at startup
  - File: `FSTRaK/Views/MainWindow.xaml.cs`
  - Action: After the existing `MapTilerMapTileLayer.ApiKey = maptillerApiKey;` line (line ~79), add:
    ```csharp
    OpenAipMapTileLayer.ApiKey = Properties.Settings.Default.OpenAipApiKey;
    ```

### Acceptance Criteria

- [ ] **AC 1**: Given the Settings view is open, when the user looks at the map section, then they see three distinct controls: a "Map tiles Provider" dropdown (base maps only), a "Chart Overlay" dropdown (with "None" as first option), and an "OpenAIP Overlay" checkbox — FAA/DFS/Israel AIP entries do NOT appear in the base layer dropdown.

- [ ] **AC 2**: Given a base map is selected (e.g., OpenStreetMap) and Chart Overlay is "None" and OpenAIP is unchecked, when the map renders, then only the base layer is visible — no aviation overlay is applied.

- [ ] **AC 3**: Given any base map is selected and a chart overlay (e.g., "FAA VFR Sectional") is selected, when the map renders, then the chart overlay appears on top of the base map without replacing it.

- [ ] **AC 4**: Given any base map is selected and OpenAIP is checked (with valid API key), when the map renders, then the OpenAIP layer appears above the base map.

- [ ] **AC 5**: Given base map is selected, OpenAIP is enabled, and a chart overlay is selected, when the map renders, then the stacking order is: base (bottom) → OpenAIP → chart overlay (top).

- [ ] **AC 6**: Given OpenAIP checkbox is unchecked, when the user views the settings, then the OpenAIP API key field is hidden. Given the checkbox is checked, then the API key field becomes visible.

- [ ] **AC 7**: Given settings are saved and the application is restarted, when the Settings view loads, then the previously selected base map, chart overlay, and OpenAIP enabled state are all restored correctly.

- [ ] **AC 8**: Given a chart overlay is active in the live map view, when the user changes the chart overlay selection in Settings, then the live map updates immediately without requiring an app restart.

- [ ] **AC 9**: Given a base map that previously required an OSM fallback (e.g., old IOverlayMapTileLayer behavior), when the user selects any base map from the base dropdown, then the map uses that selection as the base layer directly — no automatic OSM insertion occurs.

- [ ] **AC 10**: Given the map attribution text is displayed, when multiple layers are active (base + OpenAIP + chart overlay), then the attribution shows all active layer credits separated by " | ".

## Additional Context

### Dependencies

- `XAML.MapControl.WPF v13.4` — `MapBase`, `MapTileLayer`, `MapTileLayerBase` types already in use; no version change needed
- OpenAIP free API key — user must register at https://www.openaip.net to obtain one; the field is optional (layer simply won't load tiles without a valid key)
- No new NuGet packages required

### Testing Strategy

Manual verification steps (no automated tests in project):

1. Build and launch in Debug mode
2. Open Settings → verify base layer dropdown contains only non-chart maps (OSM variants, Azure, MapTiler, OpenTopoMap, TopPlus)
3. Verify Chart Overlay dropdown contains: None, DFS, FAA VFR Sectional, FAA VFR Terminal, FAA IFR Low, FAA IFR High, AIP Israel CVFR, AIP Israel LSA, AIP Israel ATS Routes, AIP Israel Helicopter Routes
4. Select OpenStreetMap base + FAA VFR Sectional overlay → confirm FAA chart renders over OSM
5. Enable OpenAIP (enter a valid key) + FAA overlay → confirm three-layer stack renders in correct order
6. Toggle OpenAIP checkbox → confirm API key field shows/hides
7. Change base map while overlays are active → confirm overlays persist and base changes
8. Restart app → confirm all selections persisted
9. Open both Live view and Flight Details view → confirm both update correctly when settings change

### Notes

- **Risk**: `map.Children.IndexOf(baseLayer)` may return -1 if `MapLayer` assignment doesn't immediately add to `Children`. If this occurs, fall back to appending OpenAIP via `map.Children.Add` and handle ordering. Verify MapControl v13.4 behavior.
- **Risk**: `x:Shared="false"` on XAML resources means each access to `Application.Current.Resources["OpenAIP"]` returns a new instance. The existing code already relies on this for `GetMapProvider()`. The same instance must be tracked in `_currentOpenAipLayer` / `_currentChartLayer` to successfully remove it from `Children` later. `MapProviderResolver` methods must return the same instance per call cycle — consider caching the result within a single `UpdateMapLayers` execution.
- **Future**: The `IsMaptillerCMap` property (controls MapTiler logo visibility) in both VMs may need extending if other API-key providers need logo treatment — out of scope now.
- **Future**: OpenAIP tile URL format may change; the API key injection pattern via `{ApiKey}` placeholder in the URI template is consistent with how Azure/MapTiler work in this codebase.

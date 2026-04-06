# Open Flightmaps Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Open Flightmaps (Europe) as an AIRAC-aware aeronautical overlay toggled via a Settings checkbox, mutually exclusive with OpenAIP (Worldwide), excluded from all dropdowns.

**Architecture:** A new `OpenFlightMapsMapTileLayer` subclass computes the current AIRAC cycle and injects it into the tile URL at render time. `MapProviderResolver` exposes a single `GetAeroOverlayLayer()` that returns whichever aeronautical overlay is enabled (OFM or OpenAIP). The tile server gets a new `overlay/ofm/` route. Settings and UI follow the existing OpenAIP checkbox pattern.

**Tech Stack:** C# / .NET Framework 4.7.2, WPF, MapControl.WPF v13.4

---

## Files

| Action | File |
|--------|------|
| **Create** | `FSTRaK/Utils/OpenFlightMapsMapTileLayer.cs` |
| **Modify** | `FSTRaK/Resources/MapProvidersDictionary.xaml` |
| **Modify** | `FSTRaK/Properties/Settings.settings` |
| **Modify** | `FSTRaK/Properties/Settings.Designer.cs` |
| **Modify** | `FSTRaK/Utils/MapProviderResolver.cs` |
| **Modify** | `FSTRaK/Utils/MapLayerHelper.cs` |
| **Modify** | `FSTRaK/Views/LiveView.xaml.cs` |
| **Modify** | `FSTRaK/Views/FlightDetailsView.xaml.cs` |
| **Modify** | `FSTRaK/Views/StatisticsView.xaml.cs` |
| **Modify** | `FSTRaK/ViewModels/SettingsViewModel.cs` |
| **Modify** | `FSTRaK/Views/SettingsView.xaml` |
| **Modify** | `FSTRaK/BusinessLogic/TileServer/TileHandler.cs` |
| **Modify** | `FSTRaK/BusinessLogic/TileServer/panel.html` |

---

### Task 1: Create `OpenFlightMapsMapTileLayer` with AIRAC calculation

**Files:**
- Create: `FSTRaK/Utils/OpenFlightMapsMapTileLayer.cs`

- [ ] **Step 1: Create the file**

```csharp
using MapControl;
using System;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class OpenFlightMapsMapTileLayer : MapTileLayer
    {
        public OpenFlightMapsMapTileLayer() : base()
        {
        }

        /// <summary>
        /// Computes the current AIRAC cycle string in "YYMM" format, e.g. "2603".
        /// AIRAC epoch: 2025-01-23 = cycle 2501. Each cycle is 28 days.
        /// </summary>
        public static string GetCurrentAiracCycle()
        {
            var epoch = new DateTime(2025, 1, 23); // cycle 2501
            var today = DateTime.UtcNow.Date;

            int cycleYear = 2025;
            int cycleWithinYear = 1;
            DateTime cycleStart = epoch;

            while (cycleStart.AddDays(28) <= today)
            {
                cycleStart = cycleStart.AddDays(28);
                cycleWithinYear++;
                // When we cross into a new calendar year, reset cycle counter
                if (cycleStart.Year > cycleYear)
                {
                    cycleYear = cycleStart.Year;
                    cycleWithinYear = 1;
                }
            }

            return $"{cycleYear % 100:D2}{cycleWithinYear:D2}";
        }

        protected override Task UpdateTileLayerAsync(bool tileSourceChanged)
        {
            if (TileSource?.UriTemplate != null &&
                TileSource.UriTemplate.Contains("{AiracCycle}"))
            {
                TileSource.UriTemplate =
                    TileSource.UriTemplate.Replace("{AiracCycle}", GetCurrentAiracCycle());
            }
            return base.UpdateTileLayerAsync(tileSourceChanged);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/Utils/OpenFlightMapsMapTileLayer.cs
git commit -m "feat: add OpenFlightMapsMapTileLayer with AIRAC cycle calculation"
```

---

### Task 2: Update `MapProvidersDictionary.xaml`

**Files:**
- Modify: `FSTRaK/Resources/MapProvidersDictionary.xaml`

The current `Open Flightmaps` entry (lines 110–116) is an `OverlayMapTileLayer` which puts it in the chart overlay dropdown. Replace it with an `OpenFlightMapsMapTileLayer`, change the URL to use `{AiracCycle}`, and update descriptions. Also update the OpenAIP description to say `(Worldwide)`.

- [ ] **Step 1: Replace the `Open Flightmaps` entry**

Find this block (lines 110–116):
```xml
    <utils:OverlayMapTileLayer
        x:Key="Open Flightmaps"
        TileSource="https://nwy-tiles-api.prod.newaydata.com/tiles/{z}/{x}/{y}.png?path=2603/aero/latest"
        SourceName="OpenFlightMaps"
        Description="© [Open Flightmaps](https://openflightmaps.org/)"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
```

Replace with:
```xml
    <utils:OpenFlightMapsMapTileLayer
        x:Key="Open Flightmaps"
        TileSource="https://nwy-tiles-api.prod.newaydata.com/tiles/{z}/{x}/{y}.png?path={AiracCycle}/aero/latest"
        SourceName="OpenFlightMaps"
        Description="© [Open Flightmaps (Europe)](https://openflightmaps.org/)"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
```

- [ ] **Step 2: Update the OpenAIP description**

Find:
```xml
    <utils:OpenAipMapTileLayer
        x:Key="OpenAIP"
        TileSource="https://api.tiles.openaip.net/api/data/openaip/{z}/{x}/{y}.png?apiKey={ApiKey}"
        SourceName="OpenAIP"
        Description="© [OpenAIP](https://www.openaip.net)"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
```

Replace with:
```xml
    <utils:OpenAipMapTileLayer
        x:Key="OpenAIP"
        TileSource="https://api.tiles.openaip.net/api/data/openaip/{z}/{x}/{y}.png?apiKey={ApiKey}"
        SourceName="OpenAIP"
        Description="© [OpenAIP (Worldwide)](https://www.openaip.net)"
        UpdateWhileViewportChanging="true"
        x:Shared="false"/>
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/Resources/MapProvidersDictionary.xaml
git commit -m "feat: replace OFM overlay entry with OpenFlightMapsMapTileLayer, update descriptions"
```

---

### Task 3: Add `IsOpenFlightMapsEnabled` setting

**Files:**
- Modify: `FSTRaK/Properties/Settings.settings`
- Modify: `FSTRaK/Properties/Settings.Designer.cs`

- [ ] **Step 1: Add to `Settings.settings`**

Open `FSTRaK/Properties/Settings.settings`. After the `IsOpenAipEnabled` entry (around line 81), add:

```xml
    <Setting Name="IsOpenFlightMapsEnabled" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">False</Value>
    </Setting>
```

- [ ] **Step 2: Add to `Settings.Designer.cs`**

Open `FSTRaK/Properties/Settings.Designer.cs`. Find the `IsOpenAipEnabled` property and add a similar property immediately after it:

```csharp
[global::System.Configuration.UserScopedSettingAttribute()]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Configuration.DefaultSettingValueAttribute("False")]
public bool IsOpenFlightMapsEnabled {
    get {
        return ((bool)(this["IsOpenFlightMapsEnabled"]));
    }
    set {
        this["IsOpenFlightMapsEnabled"] = value;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/Properties/Settings.settings FSTRaK/Properties/Settings.Designer.cs
git commit -m "feat: add IsOpenFlightMapsEnabled user setting"
```

---

### Task 4: Update `MapProviderResolver` — replace `GetOpenAipLayer` with `GetAeroOverlayLayer`

**Files:**
- Modify: `FSTRaK/Utils/MapProviderResolver.cs`

- [ ] **Step 1: Replace the method**

Replace the entire `GetOpenAipLayer` method with `GetAeroOverlayLayer`:

```csharp
public static MapTileLayerBase GetAeroOverlayLayer()
{
    if (Properties.Settings.Default.IsOpenAipEnabled)
        return Application.Current.Resources["OpenAIP"] as MapTileLayerBase;
    if (Properties.Settings.Default.IsOpenFlightMapsEnabled)
        return Application.Current.Resources["Open Flightmaps"] as MapTileLayerBase;
    return null;
}
```

The full file should now be:

```csharp
using System.Windows;
using MapControl;

namespace FSTRaK.Utils
{
    public class MapProviderResolver
    {
        public static MapTileLayerBase GetMapProvider()
        {
            var resourceKey = Properties.Settings.Default.MapTileProvider;
            var resource = Application.Current.Resources[resourceKey] as MapTileLayerBase;
            if (resource != null)
                return resource;

            return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
        }

        public static MapTileLayerBase GetChartOverlayProvider()
        {
            var key = Properties.Settings.Default.ChartOverlayProvider;
            if (string.IsNullOrEmpty(key) || key == "None") return null;
            return Application.Current.Resources[key] as MapTileLayerBase;
        }

        public static MapTileLayerBase GetAeroOverlayLayer()
        {
            if (Properties.Settings.Default.IsOpenAipEnabled)
                return Application.Current.Resources["OpenAIP"] as MapTileLayerBase;
            if (Properties.Settings.Default.IsOpenFlightMapsEnabled)
                return Application.Current.Resources["Open Flightmaps"] as MapTileLayerBase;
            return null;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/Utils/MapProviderResolver.cs
git commit -m "feat: replace GetOpenAipLayer with GetAeroOverlayLayer supporting OFM"
```

---

### Task 5: Update `MapLayerHelper` to use `GetAeroOverlayLayer`

**Files:**
- Modify: `FSTRaK/Utils/MapLayerHelper.cs`

- [ ] **Step 1: Rewrite the file**

```csharp
using MapControl;

namespace FSTRaK.Utils
{
    public static class MapLayerHelper
    {
        public static void UpdateMapLayers(
            MapBase map,
            ref MapTileLayerBase currentAeroOverlayLayer,
            ref MapTileLayerBase currentChartLayer)
        {
            // Tear down existing overlay layers
            if (currentAeroOverlayLayer != null)
            {
                map.Children.Remove(currentAeroOverlayLayer);
                currentAeroOverlayLayer = null;
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

            // Determine insertion point: just after the base layer
            var baseIndex = map.Children.IndexOf(baseLayer);
            var insertAt = baseIndex >= 0 ? baseIndex + 1 : 0;

            // Insert aero overlay (OpenAIP or OFM) above base
            var aeroLayer = MapProviderResolver.GetAeroOverlayLayer();
            if (aeroLayer != null)
            {
                map.Children.Insert(insertAt, aeroLayer);
                currentAeroOverlayLayer = aeroLayer;
                insertAt++;
            }

            // Insert chart overlay above aero overlay (or above base)
            var chartLayer = MapProviderResolver.GetChartOverlayProvider();
            if (chartLayer != null)
            {
                map.Children.Insert(insertAt, chartLayer);
                currentChartLayer = chartLayer;
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/Utils/MapLayerHelper.cs
git commit -m "feat: update MapLayerHelper to use GetAeroOverlayLayer"
```

---

### Task 6: Update call sites — `LiveView.xaml.cs`, `FlightDetailsView.xaml.cs`, `StatisticsView.xaml.cs`

These three files each hold a `_currentOpenAipLayer` field that is passed by `ref` to `MapLayerHelper.UpdateMapLayers`. Rename it to `_currentAeroOverlayLayer` in all three.

**Files:**
- Modify: `FSTRaK/Views/LiveView.xaml.cs`
- Modify: `FSTRaK/Views/FlightDetailsView.xaml.cs`
- Modify: `FSTRaK/Views/StatisticsView.xaml.cs`

- [ ] **Step 1: Update `LiveView.xaml.cs`**

Find every occurrence of `_currentOpenAipLayer` (there are 4: declaration, null-check, remove-child, and the ref argument) and rename to `_currentAeroOverlayLayer`.

Line 26 — field declaration:
```csharp
// Before:
private MapTileLayerBase _currentOpenAipLayer;
// After:
private MapTileLayerBase _currentAeroOverlayLayer;
```

Lines 58–61 — manual teardown block:
```csharp
// Before:
if (_currentOpenAipLayer != null)
{
    xMap.Children.Remove(_currentOpenAipLayer);
    _currentOpenAipLayer = null;
}
// After:
if (_currentAeroOverlayLayer != null)
{
    xMap.Children.Remove(_currentAeroOverlayLayer);
    _currentAeroOverlayLayer = null;
}
```

Line 108 — the `UpdateMapLayers` call:
```csharp
// Before:
MapLayerHelper.UpdateMapLayers(xMap, ref _currentOpenAipLayer, ref _currentChartLayer);
// After:
MapLayerHelper.UpdateMapLayers(xMap, ref _currentAeroOverlayLayer, ref _currentChartLayer);
```

- [ ] **Step 2: Update `FlightDetailsView.xaml.cs`**

Same rename — `_currentOpenAipLayer` → `_currentAeroOverlayLayer`. Occurrences:

Line 24 — field declaration:
```csharp
private MapTileLayerBase _currentAeroOverlayLayer;
```

Lines 74–77 — manual teardown:
```csharp
if (_currentAeroOverlayLayer != null)
{
    LogbookMap.Children.Remove(_currentAeroOverlayLayer);
    _currentAeroOverlayLayer = null;
}
```

Line 99 — the `UpdateMapLayers` call:
```csharp
MapLayerHelper.UpdateMapLayers(LogbookMap, ref _currentAeroOverlayLayer, ref _currentChartLayer);
```

- [ ] **Step 3: Update `StatisticsView.xaml.cs`**

Line 16 — field declaration:
```csharp
private MapTileLayerBase _currentAeroOverlayLayer;
```

Line 26 — the `UpdateMapLayers` call:
```csharp
MapLayerHelper.UpdateMapLayers(RouteMap, ref _currentAeroOverlayLayer, ref _currentChartLayer);
```

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/Views/LiveView.xaml.cs FSTRaK/Views/FlightDetailsView.xaml.cs FSTRaK/Views/StatisticsView.xaml.cs
git commit -m "feat: rename _currentOpenAipLayer to _currentAeroOverlayLayer in all view call sites"
```

---

### Task 7: Update `SettingsViewModel` — add OFM toggle with mutual exclusivity

**Files:**
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Update `IsOpenAipEnabled` setter to clear OFM when enabled**

Find the `IsOpenAipEnabled` property setter (currently around line 129):

```csharp
// Before:
set
{
    _isOpenAipEnabled = value;
    Properties.Settings.Default.IsOpenAipEnabled = _isOpenAipEnabled;
    IsShowOpenAipApiKeyField = _isOpenAipEnabled;
    OnPropertyChanged();
}
```

Replace with:
```csharp
set
{
    _isOpenAipEnabled = value;
    Properties.Settings.Default.IsOpenAipEnabled = _isOpenAipEnabled;
    IsShowOpenAipApiKeyField = _isOpenAipEnabled;
    if (_isOpenAipEnabled)
    {
        _isOpenFlightMapsEnabled = false;
        Properties.Settings.Default.IsOpenFlightMapsEnabled = false;
        OnPropertyChanged(nameof(IsOpenFlightMapsEnabled));
    }
    OnPropertyChanged();
}
```

- [ ] **Step 2: Add `IsOpenFlightMapsEnabled` property**

After the `IsShowOpenAipApiKeyField` property block (around line 160), add:

```csharp
private bool _isOpenFlightMapsEnabled;
public bool IsOpenFlightMapsEnabled
{
    get => _isOpenFlightMapsEnabled;
    set
    {
        _isOpenFlightMapsEnabled = value;
        Properties.Settings.Default.IsOpenFlightMapsEnabled = _isOpenFlightMapsEnabled;
        if (_isOpenFlightMapsEnabled)
        {
            _isOpenAipEnabled = false;
            Properties.Settings.Default.IsOpenAipEnabled = false;
            IsShowOpenAipApiKeyField = false;
            OnPropertyChanged(nameof(IsOpenAipEnabled));
        }
        OnPropertyChanged();
    }
}
```

- [ ] **Step 3: Load the new setting in `SettingsView_OnLoaded`**

Find the line that loads `IsOpenAipEnabled` (around line 461):
```csharp
IsOpenAipEnabled = Properties.Settings.Default.IsOpenAipEnabled;
```

Add immediately after:
```csharp
IsOpenFlightMapsEnabled = Properties.Settings.Default.IsOpenFlightMapsEnabled;
```

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/ViewModels/SettingsViewModel.cs
git commit -m "feat: add IsOpenFlightMapsEnabled to SettingsViewModel with mutual exclusivity"
```

---

### Task 8: Update `SettingsView.xaml` — add OFM checkbox

**Files:**
- Modify: `FSTRaK/Views/SettingsView.xaml`

- [ ] **Step 1: Update OpenAIP label to say "(Worldwide)"**

Find (line 196):
```xml
                    <StackPanel Orientation="Horizontal" Margin="10" ToolTipService.ShowDuration="5000">
                        <Label Style="{DynamicResource FSTrAkLabel}" Width="250">OpenAIP Overlay</Label>
```

Replace with:
```xml
                    <StackPanel Orientation="Horizontal" Margin="10" ToolTipService.ShowDuration="5000">
                        <Label Style="{DynamicResource FSTrAkLabel}" Width="250">OpenAIP Overlay (Worldwide)</Label>
```

- [ ] **Step 2: Add OFM checkbox row after the OpenAIP API key row**

Find the closing `</StackPanel>` of the OpenAIP API key row (after line 209 — the one with `IsShowOpenAipApiKeyField`). After that closing tag, insert:

```xml
                    <StackPanel Orientation="Horizontal" Margin="10" ToolTipService.ShowDuration="5000">
                        <Label Style="{DynamicResource FSTrAkLabel}" Width="250">Open Flightmaps (Europe)</Label>
                        <CheckBox Style="{DynamicResource SettingsCheckbox}" FontFamily="{DynamicResource CurrentFont}" FontSize="{DynamicResource ListFontSize}" VerticalAlignment="Center" IsChecked="{Binding IsOpenFlightMapsEnabled, Mode=TwoWay}"/>
                        <StackPanel.ToolTip>
                            Enable Open Flightmaps aeronautical chart overlay (Europe coverage, updated per AIRAC cycle)
                        </StackPanel.ToolTip>
                    </StackPanel>
```

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/Views/SettingsView.xaml
git commit -m "feat: add Open Flightmaps (Europe) checkbox to Settings view"
```

---

### Task 9: Update `TileHandler` — add `overlay/ofm/` route

The tile server panel uses `overlay/openaip/` to proxy OpenAIP tiles. We need a parallel `overlay/ofm/` route for OFM, and update the `SettingsViewModel` exclusion check so the OFM route works when OFM is enabled.

**Files:**
- Modify: `FSTRaK/BusinessLogic/TileServer/TileHandler.cs`

- [ ] **Step 1: Add the OFM route**

Find the `else if (route.StartsWith("overlay/openaip/", ...))` block (lines 68–84). After its closing `}`, add a new route before the final `else`:

```csharp
else if (route.StartsWith("overlay/ofm/", StringComparison.OrdinalIgnoreCase))
{
    if (!FSTRaK.Properties.Settings.Default.IsOpenFlightMapsEnabled)
    { Respond404(context); return; }

    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
    { Respond404(context); return; }

    var url = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
    {
        var p = MapProviderResolver.GetAeroOverlayLayer();
        return p == null ? null : ResolveUrl(p, z, x, y);
    });

    if (url == null) { Respond404(context); return; }
    await ServeTile(context, url, null, "OpenFlightMaps", z, x, y);
}
```

Also update the OpenAIP route to use `GetAeroOverlayLayer()` for consistency (since that method now handles which layer is active). Find (line 78):
```csharp
                        var p = MapProviderResolver.GetOpenAipLayer();
```
Replace with:
```csharp
                        var p = MapProviderResolver.GetAeroOverlayLayer();
```

Also update the `TileHandler` doc comment at the top to list the new route:
```csharp
/// <summary>
/// Handles tile HTTP requests:
///   GET /tiles/base/{z}/{x}/{y}
///   GET /tiles/overlay/chart/{z}/{x}/{y}
///   GET /tiles/overlay/openaip/{z}/{x}/{y}
///   GET /tiles/overlay/ofm/{z}/{x}/{y}
///
/// Provider objects (MapTileLayerBase) are DependencyObjects and must only be accessed
/// on the UI thread. This handler reads UriTemplate inside Dispatcher.InvokeAsync,
/// then passes the resolved URL string (and MBTiles layer ref) to TileProxyService,
/// which runs entirely off the UI thread.
/// </summary>
```

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/BusinessLogic/TileServer/TileHandler.cs
git commit -m "feat: add overlay/ofm/ tile server route for Open Flightmaps"
```

---

### Task 10: Update `panel.html` — add OFM tile layer

The in-sim panel needs to consume the new `overlay/ofm/` route alongside (or instead of) `overlay/openaip/`.

**Files:**
- Modify: `FSTRaK/BusinessLogic/TileServer/panel.html`

- [ ] **Step 1: Add the OFM tile constant and layer**

Find (around line 781):
```javascript
const TILE_OPENAIP = 'http://127.0.0.1:8765/tiles/overlay/openaip/{z}/{x}/{y}';
```

Add after it:
```javascript
const TILE_OFM     = 'http://127.0.0.1:8765/tiles/overlay/ofm/{z}/{x}/{y}';
```

Find (around line 796):
```javascript
L.tileLayer(TILE_OPENAIP, { maxZoom: 18, minZoom: 3, errorTileUrl: TRANSPARENT }).addTo(map);
```

Add after it:
```javascript
L.tileLayer(TILE_OFM,     { maxZoom: 18, minZoom: 3, errorTileUrl: TRANSPARENT }).addTo(map);
```

Note: Both layers use `errorTileUrl: TRANSPARENT`, so whichever returns 404 (the disabled one) silently renders as transparent. No conditional logic is needed in JS.

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/BusinessLogic/TileServer/panel.html
git commit -m "feat: add OFM tile layer to in-sim panel"
```

---

### Task 11: Update `SettingsViewModel` — exclude OFM from dropdowns

The `SettingsViewModel` constructor already excludes `OpenAipMapTileLayer` instances from the dropdown lists. We need to also exclude `OpenFlightMapsMapTileLayer`.

**Files:**
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Update the constructor filter**

Find (around line 428):
```csharp
                else if (provider.Value is OpenAipMapTileLayer)
                {
                    // OpenAIP is handled separately — exclude from both dropdowns
                }
```

Replace with:
```csharp
                else if (provider.Value is OpenAipMapTileLayer || provider.Value is OpenFlightMapsMapTileLayer)
                {
                    // OpenAIP and OFM are handled as separate overlays — exclude from both dropdowns
                }
```

- [ ] **Step 2: Commit**

```bash
git add FSTRaK/ViewModels/SettingsViewModel.cs
git commit -m "feat: exclude OpenFlightMapsMapTileLayer from map provider dropdowns"
```

---

## Self-Review

**Spec coverage check:**
- ✅ `OpenFlightMapsMapTileLayer` with AIRAC calc → Task 1
- ✅ XAML entry replaced, OFM removed from overlay dropdown → Task 2
- ✅ Label renames (Worldwide / Europe) → Tasks 2 & 8
- ✅ `IsOpenFlightMapsEnabled` setting → Task 3
- ✅ `GetAeroOverlayLayer` replaces `GetOpenAipLayer` → Task 4
- ✅ `MapLayerHelper` updated → Task 5
- ✅ All three view call sites updated → Task 6
- ✅ SettingsViewModel mutual exclusivity → Tasks 7 & 11
- ✅ SettingsView checkbox → Task 8
- ✅ Tile server OFM route → Task 9
- ✅ Panel.html OFM layer → Task 10

**Type consistency check:**
- `GetAeroOverlayLayer()` defined in Task 4, used in Tasks 5, 6 (via MapLayerHelper), 9
- `_currentAeroOverlayLayer` renamed consistently across Tasks 5 & 6
- `IsOpenFlightMapsEnabled` defined in Tasks 3 & 7, loaded in Task 7, bound in Task 8
- `OpenFlightMapsMapTileLayer` created in Task 1, referenced in Tasks 2 & 11

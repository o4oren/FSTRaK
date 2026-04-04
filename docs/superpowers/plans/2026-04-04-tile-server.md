# FSTRaK Tile Server — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an HTTP tile server to FSTRaK that serves the currently selected map tiles (web providers and local MBTiles) and ATC network state to external clients such as an MSFS tablet panel.

**Architecture:** `TileServer` is a singleton `HttpListener` started from `MainWindow.OnLoad` and stopped on `Application.Exit`. It dispatches to `TileHandler` (tile serving), `NetworkStateHandler` (GeoJSON ATC state), and `NetworkToggleHandler` (toggle ATC on/off). `TileProxyService` handles upstream fetching with a 500-entry LRU cache. `LiveViewViewModel` is exposed as a static property on `App` so the handlers can reach it without circular dependencies.

**Tech Stack:** C# .NET Framework 4.7.2, WPF, `System.Net.HttpListener`, `System.Net.Http.HttpClient`, `Newtonsoft.Json`, `System.Data.SQLite` (already in project)

> **Note:** No automated test suite. This project can only be built/run on Windows. Each task ends with manual verification steps.

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `FSTRaK/BusinessLogic/TileServer/TileServer.cs` | Create | Singleton HttpListener, lifecycle, routing |
| `FSTRaK/BusinessLogic/TileServer/TileProxyService.cs` | Create | Upstream fetch + LRU cache for web providers; MBTiles read |
| `FSTRaK/BusinessLogic/TileServer/TileHandler.cs` | Create | Serve `/tiles/base/`, `/tiles/overlay/chart/`, `/tiles/overlay/openaip/` |
| `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs` | Create | `GET /network/state` → GeoJSON |
| `FSTRaK/BusinessLogic/TileServer/NetworkToggleHandler.cs` | Create | `POST /network/atc/toggle` |
| `FSTRaK/App.xaml.cs` | Modify | Expose `LiveViewViewModel` static property; start/stop TileServer |
| `FSTRaK/Views/MainWindow.xaml.cs` | Modify | Start TileServer in `OnLoad`; stop on close |
| `FSTRaK/ViewModels/MainWindowViewModel.cs` | Modify | Expose `LiveViewViewModel` publicly |
| `FSTRaK/ViewModels/SettingsViewModel.cs` | Modify | Add `TileServerPort` property |
| `FSTRaK/Views/SettingsView.xaml` | Modify | Add port field + status indicator row |
| `FSTRaK/Properties/Settings.settings` | Modify | Add `TileServerPort` setting (default 8765) |

---

### Task 1: Expose `LiveViewViewModel` for external access

The `NetworkStateHandler` and `NetworkToggleHandler` need to reach `LiveViewViewModel`. It is currently a private field in `MainWindowViewModel`. We expose it via a public property on `MainWindowViewModel`, then store it as a static property on `App` when the main window loads.

**Files:**
- Modify: `FSTRaK/ViewModels/MainWindowViewModel.cs`
- Modify: `FSTRaK/App.xaml.cs`

- [ ] **Step 1: Add public `LiveViewViewModel` property to `MainWindowViewModel`**

In `FSTRaK/ViewModels/MainWindowViewModel.cs`, the field `_liveViewViewModel` is already declared on line 17. Add a public property just below it (after line 17):

```csharp
public LiveViewViewModel LiveViewViewModel => _liveViewViewModel;
```

- [ ] **Step 2: Add static `LiveViewViewModel` property to `App`**

In `FSTRaK/App.xaml.cs`, add after the `DbWarmupTask` property (after line 27):

```csharp
internal static LiveViewViewModel LiveViewViewModel { get; set; }
```

Add the required using at the top of the file (after existing usings):

```csharp
using FSTRaK.ViewModels;
```

- [ ] **Step 3: Set the static property when the main window data context is available**

In `FSTRaK/Views/MainWindow.xaml.cs`, in the `OnLoad` method (line 31), add at the top of the method body (after `_flightManager.Initialize();`):

```csharp
if (DataContext is ViewModels.MainWindowViewModel mainVm)
    App.LiveViewViewModel = mainVm.LiveViewViewModel;
```

- [ ] **Step 4: Build and verify**

Open `FSTRaK.sln` in Visual Studio. Build `Debug|x64`. Confirm zero errors.

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/ViewModels/MainWindowViewModel.cs FSTRaK/App.xaml.cs FSTRaK/Views/MainWindow.xaml.cs
git commit -m "feat: expose LiveViewViewModel for TileServer access"
```

---

### Task 2: Add `TileServerPort` setting

**Files:**
- Modify: `FSTRaK/Properties/Settings.settings`

- [ ] **Step 1: Add the setting**

In `FSTRaK/Properties/Settings.settings`, add inside the `<Settings>` element, after the last `</Setting>` tag (before `</Settings>`):

```xml
<Setting Name="TileServerPort" Type="System.Int32" Scope="User">
  <Value Profile="(Default)">8765</Value>
</Setting>
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm `Properties.Settings.Default.TileServerPort` is accessible (IntelliSense should show it as `int`).

- [ ] **Step 3: Commit**

```bash
git add "FSTRaK/Properties/Settings.settings"
git commit -m "feat: add TileServerPort setting (default 8765)"
```

---

### Task 3: `TileProxyService` — upstream fetch and LRU cache

This class resolves tile bytes from either a web provider or an MBTiles SQLite file. It is the only place that talks to the internet or reads MBTiles.

**Files:**
- Create: `FSTRaK/BusinessLogic/TileServer/TileProxyService.cs`

- [ ] **Step 1: Create the file**

Create `FSTRaK/BusinessLogic/TileServer/TileProxyService.cs`:

```csharp
using FSTRaK.Utils;
using MapControl;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// Resolves tile bytes for a given provider and {z}/{x}/{y}.
    /// Web providers: fetches upstream URL (with API key already in UriTemplate) and caches in LRU.
    /// MBTiles providers: reads SQLite directly via MBTilesTileSource.
    /// </summary>
    internal class TileProxyService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static TileProxyService()
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "FSTrAk - Flight Simulator logbook and tracker");
        }

        // LRU cache: keyed "providerKey:z/x/y" → raw PNG/JPEG bytes
        // Max 500 entries. Access order tracked via LinkedList.
        private const int CacheCapacity = 500;
        private readonly ConcurrentDictionary<string, byte[]> _cacheData = new ConcurrentDictionary<string, byte[]>();
        private readonly LinkedList<string> _cacheOrder = new LinkedList<string>();
        private readonly object _cacheLock = new object();

        public void ClearCache() 
        {
            lock (_cacheLock)
            {
                _cacheData.Clear();
                _cacheOrder.Clear();
            }
        }

        /// <summary>
        /// Returns raw tile bytes, or null if the tile is not available.
        /// providerKey is used as cache namespace (e.g. "OpenStreetMap", "CVFR").
        /// </summary>
        public async Task<byte[]> GetTileAsync(MapTileLayerBase provider, string providerKey, int z, int x, int y)
        {
            if (provider == null) return null;

            if (provider is MBTilesMapTileLayer mbLayer)
                return await GetMBTileAsync(mbLayer, z, x, y);

            return await GetWebTileAsync(provider, providerKey, z, x, y);
        }

        private async Task<byte[]> GetWebTileAsync(MapTileLayerBase provider, string providerKey, int z, int x, int y)
        {
            var cacheKey = $"{providerKey}:{z}/{x}/{y}";

            // Check cache
            lock (_cacheLock)
            {
                if (_cacheData.TryGetValue(cacheKey, out var cached))
                {
                    _cacheOrder.Remove(cacheKey);
                    _cacheOrder.AddLast(cacheKey);
                    return cached;
                }
            }

            // Resolve upstream URL from provider's UriTemplate (API key already substituted)
            var uriTemplate = provider.TileSource?.UriTemplate;
            if (string.IsNullOrEmpty(uriTemplate)) return null;

            var url = uriTemplate
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Debug("TileProxyService: upstream returned {Status} for {Url}", (int)response.StatusCode, url);
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                AddToCache(cacheKey, bytes);
                return bytes;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TileProxyService: failed to fetch {Url}", url);
                return null;
            }
        }

        private async Task<byte[]> GetMBTileAsync(MBTilesMapTileLayer layer, int z, int x, int y)
        {
            if (layer.TileSource == null) return null;
            try
            {
                var imageSource = await layer.TileSource.LoadImageAsync(x, y, z);
                if (imageSource == null) return null;

                // Encode BitmapSource → PNG bytes
                var bitmap = imageSource as BitmapSource;
                if (bitmap == null) return null;

                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TileProxyService: MBTiles read failed for {z}/{x}/{y}", z, x, y);
                return null;
            }
        }

        private void AddToCache(string key, byte[] bytes)
        {
            lock (_cacheLock)
            {
                if (_cacheData.ContainsKey(key))
                {
                    _cacheOrder.Remove(key);
                    _cacheOrder.AddLast(key);
                    _cacheData[key] = bytes;
                    return;
                }

                if (_cacheOrder.Count >= CacheCapacity)
                {
                    var oldest = _cacheOrder.First.Value;
                    _cacheOrder.RemoveFirst();
                    _cacheData.TryRemove(oldest, out _);
                }

                _cacheData[key] = bytes;
                _cacheOrder.AddLast(key);
            }
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm zero errors.

- [ ] **Step 3: Commit**

```bash
git add "FSTRaK/BusinessLogic/TileServer/TileProxyService.cs"
git commit -m "feat: add TileProxyService with LRU cache and MBTiles support"
```

---

### Task 4: `TileHandler` — serve tile endpoints

Handles the three tile routes. Reads the currently selected providers from `MapProviderResolver` and delegates to `TileProxyService`.

**Files:**
- Create: `FSTRaK/BusinessLogic/TileServer/TileHandler.cs`

- [ ] **Step 1: Create the file**

Create `FSTRaK/BusinessLogic/TileServer/TileHandler.cs`:

```csharp
using FSTRaK.Utils;
using Serilog;
using System;
using System.Net;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// Handles tile HTTP requests:
    ///   GET /tiles/base/{z}/{x}/{y}
    ///   GET /tiles/overlay/chart/{z}/{x}/{y}
    ///   GET /tiles/overlay/openaip/{z}/{x}/{y}
    /// </summary>
    internal class TileHandler
    {
        private readonly TileProxyService _proxy;

        public TileHandler(TileProxyService proxy)
        {
            _proxy = proxy;
        }

        public async Task HandleAsync(HttpListenerContext context, string route)
        {
            // route examples: "base/5/12/10"  "overlay/chart/5/12/10"  "overlay/openaip/5/12/10"
            var parts = route.TrimStart('/').Split('/');

            bool isMBTiles = false;
            MapControl.MapTileLayerBase provider = null;
            string providerKey = null;

            try
            {
                // Determine provider type from route prefix
                if (route.StartsWith("base/", StringComparison.OrdinalIgnoreCase))
                {
                    // parts: ["base", z, x, y]
                    provider = System.Windows.Application.Current.Dispatcher.Invoke(
                        () => MapProviderResolver.GetMapProvider());
                    providerKey = FSTRaK.Properties.Settings.Default.MapTileProvider;

                    if (!TryParseZXY(parts, 1, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    await ServeTile(context, provider, providerKey, z, x, y);
                }
                else if (route.StartsWith("overlay/chart/", StringComparison.OrdinalIgnoreCase))
                {
                    // parts: ["overlay", "chart", z, x, y]
                    provider = System.Windows.Application.Current.Dispatcher.Invoke(
                        () => MapProviderResolver.GetChartOverlayProvider());
                    if (provider == null) { Respond404(context); return; }
                    providerKey = FSTRaK.Properties.Settings.Default.ChartOverlayProvider;

                    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    await ServeTile(context, provider, providerKey, z, x, y);
                }
                else if (route.StartsWith("overlay/openaip/", StringComparison.OrdinalIgnoreCase))
                {
                    // parts: ["overlay", "openaip", z, x, y]
                    if (!FSTRaK.Properties.Settings.Default.IsOpenAipEnabled)
                    { Respond404(context); return; }

                    provider = System.Windows.Application.Current.Dispatcher.Invoke(
                        () => MapProviderResolver.GetOpenAipLayer());
                    if (provider == null) { Respond404(context); return; }
                    providerKey = "OpenAIP";

                    if (!TryParseZXY(parts, 2, out int z, out int x, out int y))
                    { Respond404(context); return; }

                    await ServeTile(context, provider, providerKey, z, x, y);
                }
                else
                {
                    Respond404(context);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TileHandler: unhandled error for route {Route}", route);
                Respond500(context);
            }
        }

        private async Task ServeTile(HttpListenerContext context, MapControl.MapTileLayerBase provider,
            string providerKey, int z, int x, int y)
        {
            var bytes = await _proxy.GetTileAsync(provider, providerKey, z, x, y);
            if (bytes == null || bytes.Length == 0)
            {
                Respond404(context);
                return;
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = "image/png";
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        private static bool TryParseZXY(string[] parts, int offset, out int z, out int x, out int y)
        {
            z = x = y = 0;
            return parts.Length >= offset + 3
                && int.TryParse(parts[offset], out z)
                && int.TryParse(parts[offset + 1], out x)
                && int.TryParse(parts[offset + 2], out y);
        }

        private static void Respond404(HttpListenerContext context)
        {
            context.Response.StatusCode = 404;
            context.Response.OutputStream.Close();
        }

        private static void Respond500(HttpListenerContext context)
        {
            context.Response.StatusCode = 500;
            context.Response.OutputStream.Close();
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm zero errors.

- [ ] **Step 3: Commit**

```bash
git add "FSTRaK/BusinessLogic/TileServer/TileHandler.cs"
git commit -m "feat: add TileHandler for base and overlay tile endpoints"
```

---

### Task 5: `NetworkStateHandler` — GeoJSON ATC state

**Files:**
- Create: `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs`

- [ ] **Step 1: Create the file**

Create `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs`:

```csharp
using FSTRaK.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MapControl;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// GET /network/state
    /// Returns current ATC visibility, active network, and FIR/UIR polygons as GeoJSON features.
    /// </summary>
    internal class NetworkStateHandler
    {
        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var lvm = App.LiveViewViewModel;

                JObject response;

                if (lvm == null)
                {
                    response = BuildEmptyResponse();
                }
                else
                {
                    response = Application.Current.Dispatcher.Invoke(() => BuildResponse(lvm));
                }

                var json = response.ToString(Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NetworkStateHandler: error building response");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.OutputStream.Close();
            }

            return Task.CompletedTask;
        }

        private static JObject BuildResponse(LiveViewViewModel lvm)
        {
            var features = new JArray();

            // VATSIM FIRs
            if (lvm.IsVatsimActive && lvm.IsShowVatsimAtc)
            {
                foreach (var fir in lvm.VatsimControlledFirs)
                {
                    foreach (var locations in fir.Locations ?? new System.Collections.Generic.List<LocationCollection>())
                    {
                        var feature = BuildPolygonFeature(locations, fir.Callsign, GetFirstFrequency(fir.Controllers));
                        if (feature != null) features.Add(feature);
                    }
                }

                foreach (var uir in lvm.VatsimControlledUirs)
                {
                    foreach (var locations in uir.FirLocations ?? new System.Collections.Generic.List<LocationCollection>())
                    {
                        var feature = BuildPolygonFeature(locations, uir.Callsign, GetFirstFrequency(uir.Controllers));
                        if (feature != null) features.Add(feature);
                    }
                }
            }

            // IVAO CTR polygons
            if (lvm.IsIvaoActive && lvm.IsShowIvaoAtc)
            {
                foreach (var atc in lvm.IvaoAtcList)
                {
                    if (atc.ControlPolygon != null && atc.ControlPolygon.Count > 0)
                    {
                        var feature = BuildPolygonFeature(atc.ControlPolygon, atc.Callsign, null);
                        if (feature != null) features.Add(feature);
                    }
                }
            }

            string network = "none";
            if (lvm.IsVatsimActive) network = "vatsim";
            else if (lvm.IsIvaoActive) network = "ivao";

            return new JObject
            {
                ["atcVisible"] = lvm.IsShowVatsimAtc || lvm.IsShowIvaoAtc,
                ["network"] = network,
                ["firs"] = features
            };
        }

        private static JObject BuildPolygonFeature(LocationCollection locations, string callsign, string frequency)
        {
            if (locations == null || locations.Count < 3) return null;

            var ring = new JArray();
            foreach (var loc in locations)
                ring.Add(new JArray(loc.Longitude, loc.Latitude));

            // Close the ring if not already closed
            if (locations.Count > 0)
            {
                var first = locations[0];
                ring.Add(new JArray(first.Longitude, first.Latitude));
            }

            var props = new JObject { ["callsign"] = callsign };
            if (frequency != null) props["frequency"] = frequency;

            return new JObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JObject
                {
                    ["type"] = "Polygon",
                    ["coordinates"] = new JArray(ring)
                },
                ["properties"] = props
            };
        }

        private static string GetFirstFrequency(IEnumerable<FSTRaK.BusinessLogic.VatsimService.VatsimModel.Controller> controllers)
        {
            foreach (var c in controllers)
                return c.frequency;
            return null;
        }

        private static JObject BuildEmptyResponse() =>
            new JObject
            {
                ["atcVisible"] = false,
                ["network"] = "none",
                ["firs"] = new JArray()
            };
    }
}
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm zero errors. If `lvm.IsVatsimActive` or `lvm.IsIvaoActive` are not accessible (internal visibility), check `LiveViewViewModel`'s class access modifier — if the class is `internal`, accessing it from a different namespace (`BusinessLogic.TileServer`) requires them to be in the same assembly, which they are (single project). Confirm no visibility errors.

- [ ] **Step 3: Commit**

```bash
git add "FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs"
git commit -m "feat: add NetworkStateHandler — GeoJSON ATC state endpoint"
```

---

### Task 6: `NetworkToggleHandler` — toggle ATC visibility

**Files:**
- Create: `FSTRaK/BusinessLogic/TileServer/NetworkToggleHandler.cs`

- [ ] **Step 1: Create the file**

Create `FSTRaK/BusinessLogic/TileServer/NetworkToggleHandler.cs`:

```csharp
using FSTRaK.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// POST /network/atc/toggle
    /// Toggles IsShowAtc in LiveViewViewModel and returns the new state.
    /// </summary>
    internal class NetworkToggleHandler
    {
        public Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                var lvm = App.LiveViewViewModel;
                bool newValue = false;

                if (lvm != null)
                {
                    newValue = Application.Current.Dispatcher.Invoke(() =>
                    {
                        lvm.IsShowAtc = !lvm.IsShowAtc;
                        return lvm.IsShowAtc;
                    });
                }

                var json = new JObject { ["atcVisible"] = newValue }.ToString(Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "NetworkToggleHandler: error toggling ATC");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.OutputStream.Close();
            }

            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm zero errors. If `IsShowAtc` setter is not accessible (it is `public` in `LiveViewViewModel`), no issue. If `internal`, it is still accessible from the same assembly.

- [ ] **Step 3: Commit**

```bash
git add "FSTRaK/BusinessLogic/TileServer/NetworkToggleHandler.cs"
git commit -m "feat: add NetworkToggleHandler — POST /network/atc/toggle"
```

---

### Task 7: `TileServer` — HttpListener lifecycle and routing

This is the core class. It owns the listener loop, parses URLs, and dispatches to handlers.

**Files:**
- Create: `FSTRaK/BusinessLogic/TileServer/TileServer.cs`

- [ ] **Step 1: Create the file**

Create `FSTRaK/BusinessLogic/TileServer/TileServer.cs`:

```csharp
using Serilog;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FSTRaK.BusinessLogic.TileServer
{
    /// <summary>
    /// Singleton HTTP tile server. Starts on app launch, stops on app exit.
    /// Listens on http://localhost:{port}/ (default 8765).
    /// </summary>
    internal sealed class TileServer
    {
        private static readonly object Lock = new object();
        private static TileServer _instance;

        public static TileServer Instance
        {
            get
            {
                lock (Lock)
                    return _instance ?? (_instance = new TileServer());
            }
        }

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private readonly TileProxyService _proxy = new TileProxyService();
        private TileHandler _tileHandler;
        private readonly NetworkStateHandler _networkStateHandler = new NetworkStateHandler();
        private readonly NetworkToggleHandler _networkToggleHandler = new NetworkToggleHandler();

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }

        private TileServer() { }

        public void Start()
        {
            Port = FSTRaK.Properties.Settings.Default.TileServerPort;
            _tileHandler = new TileHandler(_proxy);

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");

            try
            {
                _listener.Start();
                IsRunning = true;
                Log.Information("TileServer: listening on http://localhost:{Port}/", Port);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Log.Error(ex, "TileServer: failed to start on port {Port}", Port);
                return;
            }

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            IsRunning = false;
            Log.Information("TileServer: stopped.");
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    // Listener was stopped — exit loop
                    break;
                }

                // Handle OPTIONS preflight (CORS)
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    context.Response.StatusCode = 204;
                    context.Response.OutputStream.Close();
                    continue;
                }

                _ = Task.Run(() => DispatchAsync(context));
            }
        }

        private async Task DispatchAsync(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url.AbsolutePath.TrimStart('/');
                var method = context.Request.HttpMethod.ToUpperInvariant();

                if (path.StartsWith("tiles/", StringComparison.OrdinalIgnoreCase))
                {
                    // route = everything after "tiles/"
                    var route = path.Substring("tiles/".Length);
                    await _tileHandler.HandleAsync(context, route);
                }
                else if (path.Equals("network/state", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await _networkStateHandler.HandleAsync(context);
                }
                else if (path.Equals("network/atc/toggle", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    await _networkToggleHandler.HandleAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TileServer: unhandled error dispatching request");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.OutputStream.Close();
                }
                catch { }
            }
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm zero errors.

- [ ] **Step 3: Commit**

```bash
git add "FSTRaK/BusinessLogic/TileServer/TileServer.cs"
git commit -m "feat: add TileServer singleton HttpListener with routing"
```

---

### Task 8: Wire `TileServer` into app lifecycle

Start the server after the window loads (so API keys and map providers are initialised), stop it on app exit.

**Files:**
- Modify: `FSTRaK/Views/MainWindow.xaml.cs`
- Modify: `FSTRaK/App.xaml.cs`

- [ ] **Step 1: Start `TileServer` in `MainWindow.OnLoad`**

In `FSTRaK/Views/MainWindow.xaml.cs`, add a using at the top:

```csharp
using FSTRaK.BusinessLogic.TileServer;
```

In `OnLoad`, after the existing `TileImageLoader.Cache` line (line 83), add:

```csharp
TileServer.Instance.Start();
```

- [ ] **Step 2: Stop `TileServer` on app exit**

In `FSTRaK/App.xaml.cs`, in `OnApplicationExit` (line 121), add before `FSTRaK.Properties.Settings.Default.Save();`:

```csharp
BusinessLogic.TileServer.TileServer.Instance.Stop();
```

- [ ] **Step 3: Build and verify**

Build `Debug|x64`. Confirm zero errors.

- [ ] **Step 4: Smoke test in browser**

Run the app in `Debug|x64`. Open a browser and navigate to `http://localhost:8765/tiles/base/5/20/13` (a valid OSM tile). Confirm an image is returned. Navigate to `http://localhost:8765/network/state` and confirm a JSON response with `atcVisible`, `network`, and `firs` fields.

- [ ] **Step 5: Commit**

```bash
git add FSTRaK/Views/MainWindow.xaml.cs FSTRaK/App.xaml.cs
git commit -m "feat: start/stop TileServer in app lifecycle"
```

---

### Task 9: Settings UI — port field and status indicator

**Files:**
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`
- Modify: `FSTRaK/Views/SettingsView.xaml`

- [ ] **Step 1: Add `TileServerPort` and `TileServerStatus` to `SettingsViewModel`**

In `FSTRaK/ViewModels/SettingsViewModel.cs`, add the following properties. Find the existing properties block (look for another `int` setting property like `Units`) and add alongside them:

```csharp
public int TileServerPort
{
    get => Properties.Settings.Default.TileServerPort;
    set
    {
        if (value < 1024 || value > 65535) return;
        Properties.Settings.Default.TileServerPort = value;
        OnPropertyChanged();
    }
}

public string TileServerStatus
{
    get
    {
        var ts = BusinessLogic.TileServer.TileServer.Instance;
        return ts.IsRunning
            ? $"● Running on http://localhost:{ts.Port}/"
            : "● Failed to start — port in use";
    }
}

public bool TileServerIsRunning => BusinessLogic.TileServer.TileServer.Instance.IsRunning;
```

- [ ] **Step 2: Add the port row and status to SettingsView.xaml**

In `FSTRaK/Views/SettingsView.xaml`, find the last `<StackPanel Orientation="Horizontal" Margin="10"...` in the main settings `StackPanel` (just before the closing `</StackPanel>` around line 253). Add the following two rows after it:

```xaml
<StackPanel Orientation="Horizontal" Margin="10" ToolTipService.ShowDuration="5000">
    <Label Style="{DynamicResource FSTrAkLabel}" Width="250">Tile Server Port</Label>
    <TextBox FontFamily="{DynamicResource CurrentFont}"
             Foreground="{DynamicResource TextColor}"
             Background="{DynamicResource ControlBackgroundColorBrush}"
             FontSize="{DynamicResource ControlFontSize}"
             Width="80"
             Text="{Binding TileServerPort, UpdateSourceTrigger=PropertyChanged}"
             Cursor="Arrow" TextAlignment="Center" Padding="0 8 0 0"/>
    <TextBlock FontFamily="{DynamicResource CurrentFont}"
               FontSize="{DynamicResource ListFontSize}"
               Foreground="{DynamicResource UnselectedTextColor}"
               VerticalAlignment="Center"
               Margin="8,0,0,0"
               Text="Restart required to apply port change."/>
    <StackPanel.ToolTip>
        Port for the local tile server used by the MSFS tablet panel (default: 8765)
    </StackPanel.ToolTip>
</StackPanel>
<StackPanel Orientation="Horizontal" Margin="10,4,10,10">
    <Label Style="{DynamicResource FSTrAkLabel}" Width="250"/>
    <TextBlock FontFamily="{DynamicResource CurrentFont}"
               FontSize="{DynamicResource ListFontSize}"
               Foreground="{DynamicResource TextColor}"
               VerticalAlignment="Center"
               Text="{Binding TileServerStatus}"/>
</StackPanel>
```

- [ ] **Step 3: Build and verify**

Build `Debug|x64`. Run the app. Open Settings. Confirm "Tile Server Port" field shows `8765` and the status line shows `● Running on http://localhost:8765/`.

- [ ] **Step 4: Commit**

```bash
git add FSTRaK/ViewModels/SettingsViewModel.cs FSTRaK/Views/SettingsView.xaml
git commit -m "feat: add tile server port and status to Settings page"
```

---

### Task 10: Cache invalidation on provider change

When the user changes the selected map provider in Settings, the LRU cache should be cleared so stale tiles from the previous provider are not served.

**Files:**
- Modify: `FSTRaK/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Clear cache on provider change**

In `FSTRaK/ViewModels/SettingsViewModel.cs`, find the property setter for `SelectedMapProvider` (it persists `Properties.Settings.Default.MapTileProvider`). After saving the setting, add a cache clear call:

```csharp
BusinessLogic.TileServer.TileServer.Instance.ClearTileCache();
```

Add a `ClearTileCache()` passthrough on `TileServer` that delegates to `_proxy.ClearCache()`.

In `FSTRaK/BusinessLogic/TileServer/TileServer.cs`, add the following public method:

```csharp
public void ClearTileCache() => _proxy.ClearCache();
```

- [ ] **Step 2: Build and verify**

Build `Debug|x64`. Confirm zero errors.

- [ ] **Step 3: Commit**

```bash
git add FSTRaK/BusinessLogic/TileServer/TileServer.cs FSTRaK/ViewModels/SettingsViewModel.cs
git commit -m "feat: clear tile cache when map provider is changed in settings"
```

---

### Task 11: Manual end-to-end verification

No code changes — verification only.

- [ ] **Step 1: Build `Release|x64` and confirm zero errors**

- [ ] **Step 2: Verify tile endpoints**

Run app (`Debug|x64`). Open browser and test:
- `http://localhost:8765/tiles/base/5/20/13` — should return a PNG tile (OSM default)
- Change map to MapTiler in Settings. Reload the tile URL. Should return a MapTiler tile (different style).
- Set chart overlay to "AIP Israel CVFR" in Settings. Open `http://localhost:8765/tiles/overlay/chart/9/303/205` — should return a chart tile image (or 404 if outside coverage area, which is correct).
- With no chart overlay selected ("None"), `http://localhost:8765/tiles/overlay/chart/9/303/205` should return 404.

- [ ] **Step 3: Verify network state endpoint**

Enable VATSIM in the Live View. Open `http://localhost:8765/network/state`. Confirm JSON with `"network": "vatsim"`. Toggle ATC off in the Live View. Reload — `"atcVisible"` should be `false`.

- [ ] **Step 4: Verify toggle endpoint**

Use curl or a browser dev tools fetch to `POST http://localhost:8765/network/atc/toggle`. Confirm ATC visibility changes in the FSTRaK Live View and the JSON response reflects the new state.

```
curl -X POST http://localhost:8765/network/atc/toggle
```

Expected: `{"atcVisible":true}` (or false, depending on prior state). FSTRaK Live View ATC overlay should flip.

- [ ] **Step 5: Verify Settings page**

Open Settings. Confirm port field shows `8765` and status line shows `● Running on http://localhost:8765/`.

- [ ] **Step 6: Verify port conflict handling**

(Optional) Start another process listening on 8765. Launch FSTRaK. Confirm status shows `● Failed to start — port in use` and app continues normally without crashing.

# FSTRaK Tile Server — Design Spec

**Date:** 2026-04-04
**Status:** Approved
**Scope:** Subsystem 1 of 2 (Subsystem 2 is the MSFS 2024 tablet panel — separate spec)

---

## Problem

FSTRaK's map tile providers (OpenStreetMap, MapTiler, Azure Maps, local MBTiles) are only accessible to its own WPF map control. There is no way for an external client — such as an MSFS 2024 in-sim tablet panel — to consume the same tiles, including API-key-protected providers and locally-embedded MBTiles (e.g. Israeli VFR/IFR charts). API keys must not be exposed to external clients.

---

## Goals

- Serve map tiles over HTTP from within FSTRaK, mirroring the currently selected base map and overlays.
- Support all existing provider types: web tile providers (with API key injection), and local MBTiles (SQLite).
- Expose ATC network state (active FIR/UIR polygons) as GeoJSON for consumption by external clients.
- Allow external clients to toggle ATC overlay visibility in FSTRaK.
- Add a configurable port setting with a running status indicator in the Settings page.

---

## Architecture

A `TileServer` class lives in `FSTRaK/BusinessLogic/TileServer/`. It owns an `HttpListener`, starts on app launch (after the main window loads), and stops on app exit. Requests are dispatched to three handler classes. The server is always running — there is no manual start/stop.

```
FSTRaK process
├── TileServer                    HttpListener on :8765
│   ├── TileHandler               /tiles/base/{z}/{x}/{y}
│   │                             /tiles/overlay/chart/{z}/{x}/{y}
│   │                             /tiles/overlay/openaip/{z}/{x}/{y}
│   ├── NetworkStateHandler       GET  /network/state
│   └── NetworkToggleHandler      POST /network/atc/toggle
│
├── TileProxyService              resolves provider → fetches → caches
├── Existing: SettingsViewModel   source of selected providers + API keys
└── Existing: LiveViewViewModel   source of ATC state + FIR/UIR data
```

---

## Files

| File | Change |
|------|--------|
| `FSTRaK/BusinessLogic/TileServer/TileServer.cs` | New — HttpListener lifecycle, request dispatch |
| `FSTRaK/BusinessLogic/TileServer/TileHandler.cs` | New — tile resolution and serving |
| `FSTRaK/BusinessLogic/TileServer/TileProxyService.cs` | New — upstream fetch + LRU cache |
| `FSTRaK/BusinessLogic/TileServer/NetworkStateHandler.cs` | New — GeoJSON state endpoint |
| `FSTRaK/BusinessLogic/TileServer/NetworkToggleHandler.cs` | New — ATC toggle endpoint |
| `FSTRaK/App.xaml.cs` | Modify — start/stop TileServer |
| `FSTRaK/ViewModels/SettingsViewModel.cs` | Modify — add TileServerPort, IsRunning, RunningStatus |
| `FSTRaK/Views/SettingsView.xaml` | Modify — add port field + status indicator |
| `FSTRaK/Properties/Settings.settings` | Modify — add TileServerPort (default 8765) |

---

## Component Design

### `TileServer`

- Starts `HttpListener` on `http://localhost:{port}/` where port comes from `Properties.Settings.Default.TileServerPort` (default `8765`).
- Runs a `Task.Run` loop accepting connections. Each request is dispatched to the appropriate handler based on URL prefix.
- If `HttpListener.Start()` throws (port already in use), sets `IsRunning = false` and logs the error. No crash.
- Exposes `bool IsRunning` and `int Port` for Settings UI binding.
- `Stop()` called from `App.xaml.cs` on `Application.Exit`.

### `TileHandler`

Handles three routes, all served with `Content-Type: image/png` (or `image/jpeg` for JPEG tiles) and `Access-Control-Allow-Origin: *`.

**`GET /tiles/base/{z}/{x}/{y}`**
Resolves the currently selected base map provider via `MapProviderResolver.GetMapProvider()`. Delegates to `TileProxyService.GetTileAsync(provider, z, x, y)`.

**`GET /tiles/overlay/chart/{z}/{x}/{y}`**
Resolves the selected chart overlay via `MapProviderResolver.GetChartOverlayProvider()`. Returns `404` if no overlay is selected ("None"). Delegates to `TileProxyService.GetTileAsync(provider, z, x, y)`.

**`GET /tiles/overlay/openaip/{z}/{x}/{y}`**
Returns `404` if OpenAIP is not enabled (`Properties.Settings.Default.IsOpenAipEnabled == false`). Otherwise delegates to `TileProxyService.GetTileAsync(openAipProvider, z, x, y)`.

### `TileProxyService`

Single shared instance. Handles both provider types:

**MBTiles providers** (`MBTilesMapTileLayer`): calls `MBTilesTileSource.LoadImageAsync(x, y, z)` directly. Encodes the returned `ImageSource` to PNG bytes. No LRU cache (SQLite reads are fast).

**Web providers**: reads `provider.TileSource.UriTemplate` (a string already populated with the API key by the provider's `UpdateTileLayerAsync()`) and substitutes `{z}`, `{x}`, `{y}` to build the upstream URL. Fetches via a single shared `static HttpClient`. Returns raw response bytes.

**LRU cache** (web providers only): `ConcurrentDictionary` keyed on `"providerKey:z/x/y"`, capped at 500 entries. Evicts oldest entry when full. Cache is cleared when the user changes the selected map provider in Settings. Cache entries store raw `byte[]`.

### `NetworkStateHandler`

`GET /network/state` — reads from `LiveViewViewModel` on the UI thread via `App.Current.Dispatcher.Invoke()`. Returns:

```json
{
  "atcVisible": true,
  "network": "vatsim",
  "firs": [
    {
      "type": "Feature",
      "geometry": { "type": "Polygon", "coordinates": [[...]] },
      "properties": { "callsign": "LLLL_CTR", "frequency": "132.200" }
    }
  ]
}
```

- `network`: `"vatsim"`, `"ivao"`, or `"none"`.
- `firs`: active FIR + UIR polygons from `VatsimControlledFirs` / `VatsimControlledUirs` (or IVAO equivalents). Empty array if ATC is not visible or no network is active.
- `Content-Type: application/json`, `Access-Control-Allow-Origin: *`.

### `NetworkToggleHandler`

`POST /network/atc/toggle` — calls `App.Current.Dispatcher.Invoke()` to set `LiveViewViewModel._isShowAtc = !_isShowAtc` (via the existing `IsShowAtc` setter which handles cascading updates). Returns:

```json
{ "atcVisible": false }
```

---

## Settings Integration

**New field in Settings page:** "Tile Server Port" — numeric `TextBox` bound to `SettingsViewModel.TileServerPort` (`int`, default `8765`). Persisted to `Properties.Settings.Default.TileServerPort`. Changing the port requires an app restart; a `TextBlock` below the field reads "Restart required to apply port change."

**Status indicator:** A small coloured dot + label next to the port field, bound to `TileServer.IsRunning`:
- Green `●  Running on http://localhost:8765`
- Red `●  Failed to start — port in use`

The `TileServer` singleton is accessible from `SettingsViewModel` via a static property on `App` (same pattern as `FlightManager`).

---

## Startup / Shutdown

In `App.xaml.cs`:

```csharp
// In MainWindow.Loaded handler (after SimConnect init):
TileServer.Instance.Start();

// In Application.Exit:
TileServer.Instance.Stop();
```

`TileServer` is a singleton (double-checked locking, same pattern as `FlightManager`).

---

## Error Handling

- Port conflict on start: log error, `IsRunning = false`, app continues normally.
- Upstream tile fetch fails (network error, 4xx/5xx): return `404` to client, log at `Debug` level.
- MBTiles tile not found (outside coverage area or zoom): return `404`.
- `NetworkStateHandler` read fails (LiveViewViewModel not yet initialised): return `200` with `{ "atcVisible": false, "network": "none", "firs": [] }`.
- All handler exceptions are caught and return `500` with a plain-text body; never crash the listener loop.

---

## Out of Scope

- Serving pilot/aircraft positions (VATSIM/IVAO traffic) — ATC polygons only.
- HTTPS / authentication — local loopback only, no sensitive data beyond API keys (which never leave FSTRaK).
- Tile server for the Logbook replay map — tiles are fetched directly by the WPF map control there.
- Dynamic provider switching without restart — provider is read per-request from current settings.
- Network selection (VATSIM vs IVAO) from external clients — stays in FSTRaK's live view UI.

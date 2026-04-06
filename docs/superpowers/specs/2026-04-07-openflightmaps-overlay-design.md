# Open Flightmaps Overlay — Design Spec

**Date:** 2026-04-07

## Goal

Treat Open Flightmaps (OFM) the same way OpenAIP is treated: a dedicated aeronautical overlay toggled via a checkbox in Settings, excluded from the base-map and chart-overlay dropdowns. OFM is mutually exclusive with OpenAIP (only one aeronautical overlay active at a time). OFM's tile URL contains an AIRAC cycle component that must be auto-calculated from today's date.

---

## Architecture

### 1. `OpenFlightMapsMapTileLayer.cs` (new)

Mirrors `OpenAipMapTileLayer`. Subclasses `MapTileLayer` and overrides `UpdateTileLayerAsync` to replace the `{AiracCycle}` placeholder in the URI template with the computed current AIRAC cycle string before delegating to `base.UpdateTileLayerAsync`.

**AIRAC calculation:**
- Epoch: **2025-01-23** = cycle **2501** (first cycle of 2025)
- Each cycle is exactly **28 days**
- Algorithm: compute the absolute cycle index from the epoch, then convert to `YYMM` format where `MM` is the 1-based cycle number within the year (each year has 13 cycles, except years with an extra cycle)
- Standard approach: iterate from epoch adding 28 days per cycle, incrementing year and cycle-within-year counters, until the next cycle start exceeds today

### 2. `MapProvidersDictionary.xaml`

- Replace the existing `OverlayMapTileLayer` entry for `Open Flightmaps` with a `OpenFlightMapsMapTileLayer` entry
- URL template: `https://nwy-tiles-api.prod.newaydata.com/tiles/{z}/{x}/{y}.png?path={AiracCycle}/aero/latest`
- Resource key stays `Open Flightmaps`
- **Rename** the `OpenAIP` source/display label to `OpenAIP (Worldwide)` — only the Description text; the resource key `OpenAIP` is unchanged (used by `MapProviderResolver`)
- OFM description updated to `Open Flightmaps (Europe)`

### 3. `Settings.settings` + `Settings.Designer.cs`

Add one new `User`-scoped boolean setting:
- `IsOpenFlightMapsEnabled` — default `false`

### 4. `MapProviderResolver.cs`

- Remove `GetOpenAipLayer()`
- Add `GetAeroOverlayLayer()` — returns the OpenAIP layer if `IsOpenAipEnabled`, the OFM layer if `IsOpenFlightMapsEnabled`, or `null` if neither. (Mutual exclusivity is enforced in the ViewModel; resolver just reads settings.)

### 5. `MapLayerHelper.cs`

- Rename the `currentOpenAipLayer` parameter/field to `currentAeroOverlayLayer`
- Call `MapProviderResolver.GetAeroOverlayLayer()` instead of `GetOpenAipLayer()`
- Update all call sites (LiveView, FlightDetailsView, StatisticsView)

### 6. `SettingsViewModel.cs`

- Add `IsOpenFlightMapsEnabled` property (same pattern as `IsOpenAipEnabled`)
- When `IsOpenFlightMapsEnabled` is set to `true`, set `IsOpenAipEnabled = false` (and vice versa) to enforce mutual exclusivity
- No API key field for OFM
- Load `IsOpenFlightMapsEnabled` from settings in `SettingsView_OnLoaded()`

### 7. `SettingsView.xaml`

Add a new checkbox row immediately after the `OpenAIP (Worldwide)` row:

```
Open Flightmaps (Europe)    [checkbox]
```

Tooltip: "Enable Open Flightmaps aeronautical chart overlay (Europe coverage, updated per AIRAC cycle)"

---

## AIRAC Calculation Detail

```
epoch_date = 2025-01-23
cycle_year = 2025
cycle_within_year = 1

while (epoch_date + 28 days) <= today:
    epoch_date += 28 days
    cycle_within_year++
    if cycle_within_year > 13 (or next start crosses year boundary):
        cycle_year++
        cycle_within_year = 1

result = format("{yy:00}{mm:00}", cycle_year % 100, cycle_within_year)
```

The simpler and standard implementation: walk forward 28 days at a time from the epoch, tracking year rollovers.

---

## What Is Not Changed

- The `OpenAIP` resource key in `MapProvidersDictionary.xaml` (used internally by resolver)
- The OpenAIP API key flow
- The chart overlay dropdown (OFM is no longer an `IOverlayMapTileLayer`, so it won't appear there)
- Layer z-order: aero overlay sits between base map and chart overlay

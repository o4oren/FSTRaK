# Statistics Page Overhaul — Design Spec

**Date:** 2026-04-03  
**Branch:** new branch from `main` (not `Statistics-overhaul`, which is stale)  
**Approach:** Incremental polish — restructure existing XAML and ViewModel, add new charts and filters

---

## Goals

The current statistics page has three problems:
1. Visually dull — stats are a plain text block, not visually distinguished
2. Missing useful data — no route map, no landing rate histogram, no country breakdown
3. Poor layout hierarchy — no clear visual grouping, requires scrolling with no orientation

---

## Filters

Replace the two existing plain `ComboBox` dropdowns with three **autocomplete text-search boxes with dropdown**, all **interdependent** — selecting a value in any one filter narrows the available options in the other two.

| Filter | Source field |
|--------|-------------|
| Airline | `Aircraft.Airline` |
| Aircraft Type | `Aircraft.AircraftType` |
| Tail Number | `Aircraft.TailNumber` |

**Interdependency behaviour:** When any filter has a value, the options shown in the other two dropdowns reflect only aircraft that match the currently active filters. This requires a reactive query: whenever any filter changes, re-query the distinct values for the other two filters using the current filter state as a `WHERE` clause.

**Implementation:** Use a WPF `ComboBox` with `IsEditable="True"` and `IsTextSearchEnabled="True"`, bound to a filtered `ObservableCollection<string>`. The ViewModel exposes separate filtered lists for each filter (e.g. `FilteredAirlines`, `FilteredAircraftTypes`, `FilteredTailNumbers`) recomputed on each filter change via the existing `DebounceUpdateStatistics` pattern.

---

## Summary Stat Cards

Replace the plain `TextBlock` wall with a `WrapPanel` of styled stat cards. Each card has:
- A small uppercase label
- A large bold number
- A small unit label beneath

**7 cards (in order):**

| Card | Value | Unit |
|------|-------|------|
| Total Flights | count | — |
| Total Hours | `H:MM` | — |
| Avg Flight Time | `H:MM` | — |
| Total Distance | formatted NM | NM |
| Avg Landing v/s | average fpm | fpm |
| Total Fuel Used | weight | lbs or kg per settings |
| Total Payload | weight | lbs or kg per settings |

Weight units (Fuel, Payload) must respect the existing `UnitsUtil.GetWeightString()` helper — the same unit setting already used elsewhere in the app.

---

## Charts

All existing ScottPlot charts are **replaced with LiveCharts2** (`LiveChartsCore.SkiaSharpView.WPF` NuGet package). Verify .NET Framework 4.7.2 compatibility before implementation; if LiveCharts2 does not support 4.7.2, fall back to OxyPlot.

**Chart layout — top to bottom:**

### Full-width charts
1. **Flights per Day/Month/Year** — existing bar chart, ported to LiveCharts2. Retains the Day/Month/Year `ComboBox` selector.
2. **Route Map** — new. Uses the existing `MapControl.WPF` map control (same as `FlightDetailsView` and `LiveView`). Draws one **geodesic great-circle `MapPolyline`** per flight from `DepartureAirportDetails` to `ArrivalAirportDetails` coordinates. Uses the **currently selected map provider** from settings (same `DynamicResource` tile layer pattern as other map views). Zoom and pan enabled. Airport coordinates resolved via `AirportResolver.Instance`.

### 2-column chart rows (left | right)
| Row | Left | Right |
|-----|------|-------|
| 1 | Top Departure Airports (pie) | Top Arrival Airports (pie) |
| 2 | Top Aircraft Types (pie) | Top Airlines (pie) |
| 3 | Landing Rate Distribution (histogram) | Top Countries (pie) |

**Landing Rate Distribution:** Histogram of all `Flight.LandingFpm` values for flights where it is non-null. Bucket size: 50 fpm. X-axis range: approximately -1000 to 0 fpm.

**Top Countries:** Derived from `DepartureAirportDetails.iso_country` (departure airport, representing where you flew from). Top 5 + "Other" bucket, same pattern as existing airport/airline distributions.

All pie charts: top 5 entries + "Other", matching existing `CalculateAircraftDistribution` pattern.

---

## Architecture

**No new files.** Changes are confined to:
- `FSTRaK/Views/StatisticsView.xaml` — layout restructure, new map control, LiveCharts2 chart controls
- `FSTRaK/Views/StatisticsView.xaml.cs` — replace ScottPlot chart generation with LiveCharts2, add map polyline rendering
- `FSTRaK/ViewModels/StatisticsViewModel.cs` — add `TailNumberFilter`, `FilteredAirlines`/`FilteredAircraftTypes`/`FilteredTailNumbers` reactive filter lists, `LandingRateDistribution`, `CountryDistribution`, route data for map

**New NuGet dependency:** `LiveChartsCore.SkiaSharpView.WPF`. Must verify .NET Framework 4.7.2 compatibility as the first implementation step. If incompatible, use OxyPlot (`OxyPlot.Wpf`) as the fallback — it supports 4.7.2 and has a similar API surface for bar, pie, and histogram charts.

**Existing patterns to follow:**
- Filter debounce: existing `DebounceUpdateStatistics` / `CancellationTokenSource` pattern
- Weight formatting: `UnitsUtil.GetWeightString()`
- Map tile layer: `DynamicResource` tile layer binding from `Resources/MapProvidersDictionary.xaml`
- Chart colors: existing `ChartColor1`–`ChartColor6` resources via `ColorUtil.GetDrawingColorFromResource`

---

## Data

**Top Countries** requires resolving `iso_country` from airport details. `Airport.iso_country` is already available via `AirportResolver`. No new DB fields needed.

**Route Map** requires `latitude_deg` / `longitude_deg` from `Airport` — already available via `DepartureAirportDetails` / `ArrivalAirportDetails` on `Flight`. The ViewModel exposes a `List<(Location dep, Location arr)>` (or equivalent `MapPolyline`-friendly type) for all filtered flights.

---

## Out of Scope

- Replacing the existing `LogbookContext` or EF6 queries
- Any changes to the Logbook, Live, or Settings views
- Animations or transitions beyond what LiveCharts2 provides by default
- Export or sharing of statistics

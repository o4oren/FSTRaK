# Changes: Fix logbook flight selection not updating map path, score, and chart

## Problem

When selecting flights in the logbook view, only the first flight displayed the flight path on the map, the score details, and the altitude/speed chart. Selecting subsequent flights updated the flight info panel but the map path, scoreboard, pushpins, and chart remained stale or empty.

## Root Cause

The bug had two contributing factors:

### 1. ScottPlot crash on empty arrays (primary cause — FlightDetailsView.xaml.cs)

When a new flight is selected, events are loaded asynchronously. Before they arrive, the `Flight` setter fires `OnPropertyChanged(nameof(AltSpeedGroundAltDictionary))` to clear stale chart data. The `DataModel_OnPropertyChange` handler in `FlightDetailsView.xaml.cs` received an empty dictionary (not null), passed the `!= null` check, and called `ScottPlot.AddScatter()` with empty arrays — which **throws an exception**.

This exception propagated back through:
- `OnPropertyChanged` → `Flight` setter → `SelectedFlight` setter in `LogbookViewModel`

WPF silently swallowed the binding exception, so no error was visible. But the `SelectedFlight` setter was aborted **before** the async event loading code could run, meaning the flight events were never fetched from the database.

The first flight worked because it was selected during initial load before the `FlightDetailsView` was fully loaded and the `PropertyChanged` handler was subscribed.

### 2. ObservableCollection replacement (secondary cause — FlightDetailsViewModel.cs)

`FlightDetailsViewModel.OnFlightEventsLoaded()` replaced the `FlightPath` `ObservableCollection` with a new instance. The `MapPolyline.Locations` binding in `FlightDetailsView.xaml` held a reference to the original collection and did not re-bind to the new one.

## Changes

### FlightDetailsView.xaml.cs — chart empty-data guard

- Moved `AltSpeedChart.Plot.Clear()` **before** the null/count check so the chart always clears when switching flights.
- Added `altSpeedGroundSeries.Count > 0` to the condition, preventing `AddScatter` from being called with empty arrays.
- Added an `else` branch to call `AltSpeedChart.Refresh()` after clearing, so the UI updates immediately when there's no data yet.

### FlightDetailsViewModel.cs — collection reuse and stale data clearing

- **Flight setter**: Simplified to set `FlightDetailsParamsViewModel` (uses flight metadata, no events needed). If events are already loaded, calls `OnFlightEventsLoaded()` immediately. Otherwise clears stale data (`FlightPath.Clear()`, `MarkerList.Clear()`, `ScoreboardText = ""`) while the async event load runs.
- **OnFlightEventsLoaded()**: Uses `FlightPath.Clear()` + `Add()` loop instead of replacing the collection, keeping the `MapPolyline.Locations` binding intact.

### LogbookViewModel.cs — defensive logging and robustness

- Added debug logging throughout the `SelectedFlight` setter to trace async event loading.
- Wrapped `OnFlightEventsLoaded()` call in a try-catch to prevent future exceptions from aborting the dispatcher callback silently.
- Captured `value` into a local variable for the async closure to avoid race conditions.

### LogbookView.xaml

- Removed `IsSynchronizedWithCurrentItem="True"` from the ListView to avoid a known WPF conflict with `SelectedItem` two-way binding.

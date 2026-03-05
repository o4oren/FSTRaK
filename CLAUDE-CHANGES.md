# Changes: Fix logbook flight selection not updating map path and score

## Problem

When selecting flights in the logbook view, only the first flight displayed the flight path on the map and the score details. Selecting subsequent flights updated the flight info panel but left the map path and scoreboard stale.

## Root Cause

`FlightDetailsViewModel` replaced the `FlightPath` `ObservableCollection` with a new instance on each flight selection. The `MapPolyline.Locations` binding in `FlightDetailsView.xaml` was attached to the original collection instance and did not re-attach when the property was swapped out for a new object.

The same pattern caused `ScoreboardText` and map viewport updates to be lost — the `Flight` setter fired `OnPropertyChanged` for properties whose values were still empty (events hadn't loaded yet), and by the time `OnFlightEventsLoaded()` ran after the async event load, the binding to the new collection was not being picked up by the map control.

## Changes (FlightDetailsViewModel.cs)

### Flight setter (simplified)

- Sets `FlightDetailsParamsViewModel` (uses flight metadata, no events needed) and fires `OnPropertyChanged(nameof(Flight))`.
- If `FlightEvents` are already loaded (re-selecting a previously viewed flight), calls `OnFlightEventsLoaded()` immediately.
- Otherwise clears stale data (`FlightPath.Clear()`, `MarkerList.Clear()`, `ScoreboardText = ""`) while the async event load runs in `LogbookViewModel`.

### OnFlightEventsLoaded() (collection reuse)

- Uses `FlightPath.Clear()` followed by `FlightPath.Add()` in a loop instead of replacing the collection with a new instance. This keeps the `MapPolyline.Locations` binding connected to the same `ObservableCollection` and lets the control respond to `CollectionChanged` events.
- Guards the `ViewPort` / bounding-box calculation behind `FlightPath.Count > 0`.
- Removed redundant explicit `OnPropertyChanged` calls for `FlightPath`, `ViewPort`, and `ScoreboardText` — these already fire from their own setters or from `CollectionChanged`.

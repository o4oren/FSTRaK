# FSTRaK — UI Component Inventory

## Views

### Main Window
| Component | File | Purpose |
|-----------|------|---------|
| MainWindow | `Views/MainWindow.xaml` | Top-level container, navigation bar, view hosting |

### Primary Views (Tab Navigation)
| Component | File | ViewModel | Purpose |
|-----------|------|-----------|---------|
| LiveView | `Views/LiveView.xaml` | `LiveViewViewModel` | Real-time flight tracking with interactive map |
| LogbookView | `Views/LogbookView.xaml` | `LogbookViewModel` | Flight history list with search/filter |
| StatisticsView | `Views/StatisticsView.xaml` | `StatisticsViewModel` | Aggregate flight statistics |
| SettingsView | `Views/SettingsView.xaml` | `SettingsViewModel` | Application preferences |

### Detail Views
| Component | File | ViewModel | Purpose |
|-----------|------|-----------|---------|
| FlightDetailsView | `Views/FlightDetailsView.xaml` | `FlightDetailsViewModel` | Full flight replay, map path, scoring, altitude/speed chart |
| FlightDetailsParamsView | `Views/FlightDetailsParamsView.xaml` | `FlightDetailsParamsViewModel` | Flight telemetry parameters panel |

### Popup Dialogs
| Component | File | ViewModel | Purpose |
|-----------|------|-----------|---------|
| AddCommentPopupView | `Views/AddCommentPopupView.xaml` | `AddCommentViewModel` | Add/edit comments on flights |
| EditAircraftPopupView | `Views/EditAircraftPopupView.xaml` | `EditAircraftViewModel` | Correct aircraft type/manufacturer |

### Reusable Controls
| Component | File | Purpose |
|-----------|------|---------|
| OverlayTextCardControl | `Views/OverlayTextCardControl.xaml` | Reusable overlay card with Header/Text properties (used in LiveView) |

## ViewModels

| ViewModel | Purpose |
|-----------|---------|
| `BaseViewModel` | Abstract base — implements `INotifyPropertyChanged` |
| `MainWindowViewModel` | Navigation between 4 primary views, window position/size |
| `LiveViewViewModel` | Live map state, flight path, VATSIM overlays, aircraft position |
| `LogbookViewModel` | Flight list queries, selection, async event loading |
| `StatisticsViewModel` | Flight metrics aggregation |
| `SettingsViewModel` | Application settings two-way binding |
| `FlightDetailsViewModel` | Flight replay data, path, markers, scoreboard, chart data |
| `FlightDetailsParamsViewModel` | Formatted flight parameters display |
| `AddCommentViewModel` | Comment text and save |
| `EditAircraftViewModel` | Aircraft type correction |

## Resource Dictionaries

| Resource | File | Purpose |
|----------|------|---------|
| Theme | `Resources/Theme.xaml` | Light theme colors, brushes, styles |
| DarkTheme | `Resources/DarkTheme.xaml` | Dark theme overlay |
| ButtonsTheme | `Resources/ButtonsTheme.xaml` | Button style templates |
| Images | `Resources/Images.xaml` | Image and icon resource definitions |
| AircraftIconsDictionary | `Resources/AircraftIconsDictionary.xaml` | Aircraft type icon geometries (B737, A320, C172, B747, etc.) |
| MapProvidersDictionary | `Resources/MapProvidersDictionary.xaml` | Map tile provider configurations |

## Map Components

### Tile Layers
| Component | File | Purpose |
|-----------|------|---------|
| SkyVectorMapTileLayer | `Utils/SkyVectorMapTileLayer.cs` | SkyVector VFR/IFR chart tiles |
| SkyVectorTileSource | `Utils/SkyVectorTileSource.cs` | Fetches current AIRAC cycle, adjusts zoom |
| MapTilerMapTileLayer | `Utils/MapTilerMapTileLayer.cs` | MapTiler tiles with API key injection |
| AzureMapsMapTileLayer | `Utils/AzureMapsMapTileLayer.cs` | Azure Maps tiles with API key injection |

### Map Utilities
| Component | File | Purpose |
|-----------|------|---------|
| MapProviderResolver | `Utils/MapProviderResolver.cs` | Resolves map tile provider from settings |
| MapUtils | `Utils/MapUtils.cs` | Antimeridian wrapping for polylines/polygons |
| CoordinatesUtil | `Utils/CoordinatesUtil.cs` | Geographic centroid calculation |

## LiveView Overlay Architecture

The LiveView is the most complex UI with multiple map overlay layers:

1. **VATSIM UIRs** — Semi-transparent blue polygons (upper regions)
2. **VATSIM FIRs** — Semi-transparent green polygons (flight regions)
3. **VATSIM Airports** — Approach circles + airport icons with tooltips
4. **VATSIM Aircraft** — Rotatable aircraft graphics with callsign labels
5. **Player Flight Trail** — Polyline of recorded flight path
6. **Current Position** — Rotatable aircraft icon at live location

**Left overlay cards:** Connection status, flight state, aircraft type, flight params
**Right toggle buttons:** Center map, Show VATSIM Pilots, Show Airports, Show FIRs

## Themes

Two complete theme sets (light + dark) with consistent color palettes:
- Background, foreground, accent colors
- Control styles (buttons, text boxes, list views, combo boxes)
- Custom font support: Slopes (default), Arial, Segoe UI, Georgia, Consolas, Comic Sans, Palatino, Bahnschrift, Ink Free

Theme switching via `ResourceUtil.SetTheme()` — merges `DarkTheme.xaml` over base `Theme.xaml`.

## WPF Value Converters
| Converter | File | Purpose |
|-----------|------|---------|
| `NullToVisibilityConverter` | `Utils/Converters.cs` | Null → Hidden, non-null → Visible |
| `ResourceNameToGeometryConverter` | `Utils/Converters.cs` | Resource key → WPF Geometry |
| `ResourceNameToImageConverter` | `Utils/Converters.cs` | Resource key → BitmapImage |
| `BooleanToVisibilityConverter` | Built-in | Bool → Visibility (App.xaml) |

## Attached Behaviors
| Behavior | File | Purpose |
|----------|------|---------|
| HyperlinkText | `Utils/HyperlinkText.cs` | Converts `[text](url)` in text blocks to clickable hyperlinks |

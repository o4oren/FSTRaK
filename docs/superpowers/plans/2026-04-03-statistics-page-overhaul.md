# Statistics Page Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overhaul the Statistics page into a modern dashboard with stat cards, interdependent autocomplete filters, a route map, a landing rate histogram, and a top countries chart — replacing ScottPlot with LiveCharts2.

**Architecture:** Incremental — restructure `StatisticsView.xaml` layout and replace `StatisticsView.xaml.cs` chart rendering; extend `StatisticsViewModel.cs` with new filter/data properties. No new files, no DB changes.

**Tech Stack:** WPF, .NET Framework 4.7.2, C#, LiveCharts2 (`LiveChartsCore.SkiaSharpView.WPF`), MapControl.WPF (already in project), EF6/SQLite (existing)

---

## Pre-flight: Branch Setup

- [ ] Create and check out a new branch from `main`:
  ```bash
  git checkout main
  git checkout -b statistics-overhaul-2026
  ```

---

## Task 1: Verify LiveCharts2 compatibility and add NuGet package

**Files:**
- Modify: `FSTRaK/FSTrAk.csproj` (via NuGet Package Manager in Visual Studio)

**Context:** LiveCharts2 targets .NET 5+ officially, but `LiveChartsCore.SkiaSharpView.WPF` 2.x has a .NET Framework 4.6.2+ compatible build. Must verify before proceeding.

- [ ] **Step 1: Check LiveCharts2 NuGet compatibility**

  In Visual Studio NuGet Package Manager, search for `LiveChartsCore.SkiaSharpView.WPF`. Check the versions tab — look for a version whose "Target Frameworks" column includes `net462` or `net47` or `net472`. If available, note the version number.

  If LiveCharts2 is NOT compatible with net472, use `OxyPlot.Wpf` instead (confirmed net472 compatible). All subsequent tasks use LiveCharts2 API; if you must use OxyPlot, substitute the OxyPlot equivalents for `CartesianChart`, `PieChart`, `ISeries`, etc. OxyPlot equivalents: `PlotView` instead of `CartesianChart`/`PieChart`, `BarSeries`/`PieSeries` instead of LiveCharts2 series types.

- [ ] **Step 2: Install the package**

  In Visual Studio NuGet console:
  ```
  Install-Package LiveChartsCore.SkiaSharpView.WPF -ProjectName FSTRaK
  ```

  Confirm `packages.config` or `.csproj` references are updated.

- [ ] **Step 3: Verify build compiles cleanly**

  Build the solution (`x64|Debug`). Expected: 0 errors. Warnings about ScottPlot are fine — ScottPlot stays in the project until Task 4 removes it.

- [ ] **Step 4: Commit**
  ```bash
  git add FSTRaK/FSTrAk.csproj packages.config
  git commit -m "chore: add LiveChartsCore.SkiaSharpView.WPF NuGet package"
  ```

---

## Task 2: Extend ViewModel — new filter properties and interdependent filter logic

**Files:**
- Modify: `FSTRaK/ViewModels/StatisticsViewModel.cs`

**Context:** The current ViewModel has `AirlineFilter` and `AircraftTypeFilter` as plain strings, with `Airlines` and `AircraftTypes` as `List<string>` loaded once at startup. We need to add `TailNumberFilter` and make all three filter option lists reactive (re-queried whenever any filter changes).

- [ ] **Step 1: Add `TailNumberFilter` property**

  In `StatisticsViewModel.cs`, after the `AircraftTypeFilter` property block (around line 67), add:

  ```csharp
  private string _tailNumberFilter;
  public string TailNumberFilter
  {
      get => _tailNumberFilter;
      set
      {
          _tailNumberFilter = value;
          DebounceUpdateStatistics();
          OnPropertyChanged();
      }
  }
  ```

- [ ] **Step 2: Replace `Airlines` and `AircraftTypes` with reactive filtered collections**

  Replace the existing `List<string>` properties `Airlines` and `AircraftTypes` with `ObservableCollection<string>`, and add a matching one for tail numbers. Find the declarations around lines 21–41 and replace them:

  ```csharp
  private ObservableCollection<string> _filteredAirlines = new ObservableCollection<string>();
  public ObservableCollection<string> FilteredAirlines
  {
      get => _filteredAirlines;
      set { _filteredAirlines = value; OnPropertyChanged(); }
  }

  private ObservableCollection<string> _filteredAircraftTypes = new ObservableCollection<string>();
  public ObservableCollection<string> FilteredAircraftTypes
  {
      get => _filteredAircraftTypes;
      set { _filteredAircraftTypes = value; OnPropertyChanged(); }
  }

  private ObservableCollection<string> _filteredTailNumbers = new ObservableCollection<string>();
  public ObservableCollection<string> FilteredTailNumbers
  {
      get => _filteredTailNumbers;
      set { _filteredTailNumbers = value; OnPropertyChanged(); }
  }
  ```

  Delete the old `_aircraftTypes`, `_airlines`, `AircraftTypes`, `Airlines` fields/properties entirely.

- [ ] **Step 3: Rewrite `QueryFiltersFromDbAsync` to respect current filters**

  Replace the existing `QueryFiltersFromDbAsync` method with one that filters based on current active filters, so each dropdown only shows options compatible with the other active filters:

  ```csharp
  private async Task<(List<string> airlines, List<string> types, List<string> tailNumbers)> QueryFiltersFromDbAsync()
  {
      using var logbookContext = new LogbookContext();

      IQueryable<Models.Entity.Aircraft> query = logbookContext.Aircraft.AsNoTracking();

      if (!string.IsNullOrEmpty(AirlineFilter))
          query = query.Where(a => a.Airline == AirlineFilter);
      if (!string.IsNullOrEmpty(AircraftTypeFilter))
          query = query.Where(a => a.AircraftType == AircraftTypeFilter);
      if (!string.IsNullOrEmpty(TailNumberFilter))
          query = query.Where(a => a.TailNumber == TailNumberFilter);

      var airlinesTask = query
          .Where(a => a.Airline != null && a.Airline.Trim() != "")
          .Select(a => a.Airline).Distinct().OrderBy(a => a).ToListAsync();

      var typesTask = query
          .Where(a => a.AircraftType != null && a.AircraftType.Trim() != "")
          .Select(a => a.AircraftType).Distinct().OrderBy(t => t).ToListAsync();

      var tailsTask = query
          .Where(a => a.TailNumber != null && a.TailNumber.Trim() != "")
          .Select(a => a.TailNumber).Distinct().OrderBy(t => t).ToListAsync();

      await Task.WhenAll(airlinesTask, typesTask, tailsTask).ConfigureAwait(false);

      var airlines = airlinesTask.Result;
      if (!airlines.Contains(string.Empty)) airlines.Insert(0, string.Empty);

      var types = typesTask.Result;
      if (!types.Contains(string.Empty)) types.Insert(0, string.Empty);

      var tails = tailsTask.Result;
      if (!tails.Contains(string.Empty)) tails.Insert(0, string.Empty);

      return (airlines, types, tails);
  }
  ```

- [ ] **Step 4: Update `CreateFiltersAsync` to populate the three new collections**

  The cache DTO and logic also needs updating. Replace `FiltersCacheDto` with:

  ```csharp
  private class FiltersCacheDto
  {
      public List<string> Airlines { get; set; }
      public List<string> AircraftTypes { get; set; }
      public List<string> TailNumbers { get; set; }
      public DateTime Generated { get; set; }
  }
  ```

  Update `TryReadFiltersCache` return type to `(List<string> airlines, List<string> types, List<string> tailNumbers)?` and update `WriteFiltersCache` to accept and persist tail numbers.

  Update `CreateFiltersAsync` to dispatch to `FilteredAirlines`, `FilteredAircraftTypes`, `FilteredTailNumbers` instead of the old properties:

  ```csharp
  App.Current.Dispatcher.Invoke(() =>
  {
      FilteredAirlines = new ObservableCollection<string>(result.airlines);
      FilteredAircraftTypes = new ObservableCollection<string>(result.types);
      FilteredTailNumbers = new ObservableCollection<string>(result.tailNumbers);
  });
  ```

- [ ] **Step 5: Call `CreateFiltersAsync` inside `DebounceUpdateStatistics` so filter options refresh after each filter change**

  In `DebounceUpdateStatistics`, after the `await UpdateStatisticsAsync()` call, add:
  ```csharp
  await CreateFiltersAsync().ConfigureAwait(false);
  ```

- [ ] **Step 6: Build and verify no compile errors**

  Build `x64|Debug`. Fix any missing `using` directives (`System.Collections.ObjectModel`).

- [ ] **Step 7: Commit**
  ```bash
  git add FSTRaK/ViewModels/StatisticsViewModel.cs
  git commit -m "feat: add tail number filter and interdependent filter option lists"
  ```

---

## Task 3: Extend ViewModel — new data properties for new charts

**Files:**
- Modify: `FSTRaK/ViewModels/StatisticsViewModel.cs`

**Context:** Need to add `LandingRateDistribution` (histogram buckets), `CountryDistribution` (pie), and `FlightRoutes` (list of dep/arr location pairs for the route map).

- [ ] **Step 1: Add `LandingRateDistribution` property**

  Add after the existing `MaxLandingFpm` property block:

  ```csharp
  private List<(double bucketCenter, int count)> _landingRateDistribution;
  public List<(double bucketCenter, int count)> LandingRateDistribution
  {
      get => _landingRateDistribution;
      set { _landingRateDistribution = value; OnPropertyChanged(); }
  }
  ```

- [ ] **Step 2: Add `CountryDistribution` property**

  ```csharp
  private Dictionary<string, double> _countryDistribution;
  public Dictionary<string, double> CountryDistribution
  {
      get => _countryDistribution;
      set { _countryDistribution = value; OnPropertyChanged(); }
  }
  ```

- [ ] **Step 3: Add `FlightRoutes` property**

  Add `using MapControl;` at the top of the file, then add:

  ```csharp
  private List<(Location dep, Location arr)> _flightRoutes;
  public List<(Location dep, Location arr)> FlightRoutes
  {
      get => _flightRoutes;
      set { _flightRoutes = value; OnPropertyChanged(); }
  }
  ```

- [ ] **Step 4: Add `CalculateLandingRateDistribution` helper method**

  Add this private static method near the other `Calculate*` methods:

  ```csharp
  private static List<(double bucketCenter, int count)> CalculateLandingRateDistribution(List<Flight> flights)
  {
      const int bucketSize = 50;
      const int minFpm = -1000;
      const int maxFpm = 0;

      var buckets = new Dictionary<int, int>();
      for (int b = minFpm; b < maxFpm; b += bucketSize)
          buckets[b] = 0;

      foreach (var f in flights.Where(f => f.LandingFpm.HasValue))
      {
          var fpm = (int)f.LandingFpm.Value;
          var bucket = (int)(Math.Floor((double)fpm / bucketSize) * bucketSize);
          bucket = Math.Max(minFpm, Math.Min(maxFpm - bucketSize, bucket));
          if (buckets.ContainsKey(bucket))
              buckets[bucket]++;
      }

      return buckets
          .OrderBy(kv => kv.Key)
          .Select(kv => (bucketCenter: (double)(kv.Key + bucketSize / 2), count: kv.Value))
          .ToList();
  }
  ```

- [ ] **Step 5: Add `CalculateCountryDistribution` helper method**

  ```csharp
  private static Dictionary<string, double> CalculateCountryDistribution(List<Flight> flights)
  {
      var i = 0;
      var sum = 0;
      var dist = new Dictionary<string, double>();

      foreach (var f in flights
          .GroupBy(f => f.DepartureAirportDetails?.iso_country ?? "Unknown")
          .Select(g => new { country = g.Key, count = g.Count() })
          .OrderByDescending(x => x.count))
      {
          if (i < 5)
          {
              dist.Add(string.IsNullOrEmpty(f.country) ? "Unknown" : f.country, f.count);
              i++;
          }
          else
          {
              sum += f.count;
          }
      }

      if (sum > 0)
          dist.Add("Other", (double)sum);

      return dist;
  }
  ```

- [ ] **Step 6: Add `CalculateFlightRoutes` helper method**

  ```csharp
  private static List<(Location dep, Location arr)> CalculateFlightRoutes(List<Flight> flights)
  {
      var routes = new List<(Location, Location)>();
      foreach (var f in flights)
      {
          var dep = f.DepartureAirportDetails;
          var arr = f.ArrivalAirportDetails;
          if (dep == null || arr == null) continue;
          if (dep.latitude_deg == 0 && dep.longitude_deg == 0) continue;
          if (arr.latitude_deg == 0 && arr.longitude_deg == 0) continue;
          routes.Add((
              new Location(dep.latitude_deg, dep.longitude_deg),
              new Location(arr.latitude_deg, arr.longitude_deg)
          ));
      }
      return routes;
  }
  ```

- [ ] **Step 7: Wire up new calculations in `UpdateStatisticsAsync`**

  In `UpdateStatisticsAsync`, after the existing `var flightsPerDay = CalculateFlightsPerDay(flights);` line, add:

  ```csharp
  var landingDist = CalculateLandingRateDistribution(flights);
  var countryDist = CalculateCountryDistribution(flights);
  var flightRoutes = CalculateFlightRoutes(flights);
  ```

  In the `App.Current.Dispatcher.Invoke` block, after setting `FlightsPerDay`, add:

  ```csharp
  LandingRateDistribution = landingDist;
  CountryDistribution = countryDist;
  FlightRoutes = flightRoutes;
  ```

  Also in the empty-results branch of `UpdateStatisticsAsync`, add:

  ```csharp
  LandingRateDistribution = new List<(double, int)>();
  CountryDistribution = new Dictionary<string, double>();
  FlightRoutes = new List<(Location, Location)>();
  ```

- [ ] **Step 8: Build and verify no compile errors**

- [ ] **Step 9: Commit**
  ```bash
  git add FSTRaK/ViewModels/StatisticsViewModel.cs
  git commit -m "feat: add landing distribution, country distribution, and flight routes to StatisticsViewModel"
  ```

---

## Task 4: Rebuild StatisticsView.xaml — layout with stat cards and chart placeholders

**Files:**
- Modify: `FSTRaK/Views/StatisticsView.xaml`

**Context:** Replace the entire XAML layout. Remove ScottPlot `WpfPlot` elements and the old `TextBlock` stats wall. Add: filter bar with 3 `ComboBox`es (editable), `WrapPanel` of 7 stat cards, full-width `CartesianChart` (flights over time), full-width `map:Map` (route map), and three 2-column rows of `PieChart` / `CartesianChart` (histogram) controls from LiveCharts2.

The map uses `MapControl.WPF` exactly like `FlightDetailsView.xaml`.

- [ ] **Step 1: Replace `StatisticsView.xaml` with the new layout**

  Replace the entire file content with:

  ```xml
  <UserControl x:Class="FSTRaK.Views.StatisticsView"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
               xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
               xmlns:viewmodels="clr-namespace:FSTRaK.ViewModels"
               xmlns:System="clr-namespace:System;assembly=mscorlib"
               xmlns:dataTypes="clr-namespace:FSTRaK.DataTypes"
               xmlns:map="clr-namespace:MapControl;assembly=MapControl.WPF"
               xmlns:utils="clr-namespace:FSTRaK.Utils"
               xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF"
               d:DataContext="{d:DesignInstance Type=viewmodels:StatisticsViewModel}"
               mc:Ignorable="d"
               d:DesignHeight="900" d:DesignWidth="1000"
               Loaded="OnLoaded">

    <UserControl.Resources>
      <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
          <ResourceDictionary Source="../Resources/MapProvidersDictionary.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        <ObjectDataProvider x:Key="dataFromEnum" MethodName="GetValues" ObjectType="{x:Type System:Enum}">
          <ObjectDataProvider.MethodParameters>
            <x:Type TypeName="dataTypes:TimePeriod"/>
          </ObjectDataProvider.MethodParameters>
        </ObjectDataProvider>
      </ResourceDictionary>
    </UserControl.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
      <StackPanel Margin="10">

        <!-- Filter bar -->
        <WrapPanel Orientation="Horizontal" Margin="0,0,0,10">
          <StackPanel Orientation="Horizontal" Margin="0,0,16,4" VerticalAlignment="Center">
            <Label Style="{StaticResource FSTrAkLabel}">Airline</Label>
            <ComboBox x:Name="AirlineComboBox"
                      IsEditable="True" IsTextSearchEnabled="True"
                      FontFamily="{DynamicResource CurrentFont}"
                      Foreground="{DynamicResource TextColor}"
                      FontSize="{DynamicResource ControlFontSize}"
                      Width="180"
                      ItemsSource="{Binding FilteredAirlines}"
                      Text="{Binding AirlineFilter, UpdateSourceTrigger=PropertyChanged}"/>
          </StackPanel>
          <StackPanel Orientation="Horizontal" Margin="0,0,16,4" VerticalAlignment="Center">
            <Label Style="{StaticResource FSTrAkLabel}">Aircraft Type</Label>
            <ComboBox x:Name="AircraftTypeComboBox"
                      IsEditable="True" IsTextSearchEnabled="True"
                      FontFamily="{DynamicResource CurrentFont}"
                      Foreground="{DynamicResource TextColor}"
                      FontSize="{DynamicResource ControlFontSize}"
                      Width="180"
                      ItemsSource="{Binding FilteredAircraftTypes}"
                      Text="{Binding AircraftTypeFilter, UpdateSourceTrigger=PropertyChanged}"/>
          </StackPanel>
          <StackPanel Orientation="Horizontal" Margin="0,0,0,4" VerticalAlignment="Center">
            <Label Style="{StaticResource FSTrAkLabel}">Tail Number</Label>
            <ComboBox x:Name="TailNumberComboBox"
                      IsEditable="True" IsTextSearchEnabled="True"
                      FontFamily="{DynamicResource CurrentFont}"
                      Foreground="{DynamicResource TextColor}"
                      FontSize="{DynamicResource ControlFontSize}"
                      Width="180"
                      ItemsSource="{Binding FilteredTailNumbers}"
                      Text="{Binding TailNumberFilter, UpdateSourceTrigger=PropertyChanged}"/>
          </StackPanel>
        </WrapPanel>

        <!-- Stat cards -->
        <WrapPanel Orientation="Horizontal" Margin="0,0,0,10">
          <!-- Total Flights -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,8,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="TOTAL FLIGHTS"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding TotalNumberOfFlights}"/>
            </StackPanel>
          </Border>
          <!-- Total Hours -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,8,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="TOTAL HOURS"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding TotalFlightTime}"/>
            </StackPanel>
          </Border>
          <!-- Avg Flight Time -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,8,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="AVG FLIGHT TIME"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding AvgFlightTime}"/>
            </StackPanel>
          </Border>
          <!-- Total Distance -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,8,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="TOTAL DISTANCE"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding TotalFlightDistance}"/>
              <TextBlock Style="{StaticResource StatCardUnit}" Text="NM"/>
            </StackPanel>
          </Border>
          <!-- Avg Landing v/s -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,8,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="AVG LANDING V/S"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding AvgLandingFpm}"/>
              <TextBlock Style="{StaticResource StatCardUnit}" Text="fpm"/>
            </StackPanel>
          </Border>
          <!-- Total Fuel Used -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,8,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="TOTAL FUEL USED"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding TotalFuelUsed}"/>
            </StackPanel>
          </Border>
          <!-- Total Payload -->
          <Border Style="{StaticResource StatCard}" Margin="0,0,0,8">
            <StackPanel>
              <TextBlock Style="{StaticResource StatCardLabel}" Text="TOTAL PAYLOAD"/>
              <TextBlock Style="{StaticResource StatCardValue}" Text="{Binding TotalPayload}"/>
            </StackPanel>
          </Border>
        </WrapPanel>

        <!-- Flights over time bar chart (full width) -->
        <Border Style="{StaticResource ChartCard}" Margin="0,0,0,10">
          <StackPanel>
            <StackPanel Orientation="Horizontal" Margin="0,0,0,6">
              <Label Style="{StaticResource FSTrAkLabel}">Flights per</Label>
              <ComboBox FontFamily="{DynamicResource CurrentFont}"
                        Foreground="{DynamicResource TextColor}"
                        FontSize="{DynamicResource ControlFontSize}"
                        Width="120"
                        ItemsSource="{Binding Source={StaticResource dataFromEnum}}"
                        SelectedItem="{Binding TimePeriod}"/>
            </StackPanel>
            <lvc:CartesianChart x:Name="FlightsPerPeriodChart"
                                Series="{Binding FlightsPerPeriodSeries}"
                                XAxes="{Binding FlightsPerPeriodXAxes}"
                                YAxes="{Binding FlightsPerPeriodYAxes}"
                                MinHeight="200"/>
          </StackPanel>
        </Border>

        <!-- Route map (full width) -->
        <Border Style="{StaticResource ChartCard}" Margin="0,0,0,10">
          <StackPanel>
            <Label Style="{StaticResource FSTrAkLabel}">Route Map</Label>
            <map:Map x:Name="RouteMap"
                     ZoomLevel="2"
                     Center="30,10"
                     MapProjection="{DynamicResource WebMercatorProjection}"
                     MinHeight="300">
              <map:MapScale Opacity="0.5" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="5"/>
            </map:Map>
          </StackPanel>
        </Border>

        <!-- Row 1: Top DEP airports | Top ARR airports -->
        <Grid Margin="0,0,0,10">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="8"/>
            <ColumnDefinition Width="*"/>
          </Grid.ColumnDefinitions>
          <Border Grid.Column="0" Style="{StaticResource ChartCard}">
            <StackPanel>
              <Label Style="{StaticResource FSTrAkLabel}">Top Departure Airports</Label>
              <lvc:PieChart x:Name="DepAirportsChart"
                            Series="{Binding DepAirportsSeries}"
                            MinHeight="200" IsClockwise="False"/>
            </StackPanel>
          </Border>
          <Border Grid.Column="2" Style="{StaticResource ChartCard}">
            <StackPanel>
              <Label Style="{StaticResource FSTrAkLabel}">Top Arrival Airports</Label>
              <lvc:PieChart x:Name="ArrAirportsChart"
                            Series="{Binding ArrAirportsSeries}"
                            MinHeight="200" IsClockwise="False"/>
            </StackPanel>
          </Border>
        </Grid>

        <!-- Row 2: Top Aircraft Types | Top Airlines -->
        <Grid Margin="0,0,0,10">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="8"/>
            <ColumnDefinition Width="*"/>
          </Grid.ColumnDefinitions>
          <Border Grid.Column="0" Style="{StaticResource ChartCard}">
            <StackPanel>
              <Label Style="{StaticResource FSTrAkLabel}">Top Aircraft Types</Label>
              <lvc:PieChart x:Name="AircraftTypesChart"
                            Series="{Binding AircraftTypesSeries}"
                            MinHeight="200" IsClockwise="False"/>
            </StackPanel>
          </Border>
          <Border Grid.Column="2" Style="{StaticResource ChartCard}">
            <StackPanel>
              <Label Style="{StaticResource FSTrAkLabel}">Top Airlines</Label>
              <lvc:PieChart x:Name="AirlinesChart"
                            Series="{Binding AirlinesSeries}"
                            MinHeight="200" IsClockwise="False"/>
            </StackPanel>
          </Border>
        </Grid>

        <!-- Row 3: Landing Rate Distribution | Top Countries -->
        <Grid Margin="0,0,0,10">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="8"/>
            <ColumnDefinition Width="*"/>
          </Grid.ColumnDefinitions>
          <Border Grid.Column="0" Style="{StaticResource ChartCard}">
            <StackPanel>
              <Label Style="{StaticResource FSTrAkLabel}">Landing Rate Distribution</Label>
              <lvc:CartesianChart x:Name="LandingRateChart"
                                  Series="{Binding LandingRateSeries}"
                                  XAxes="{Binding LandingRateXAxes}"
                                  YAxes="{Binding LandingRateYAxes}"
                                  MinHeight="200"/>
            </StackPanel>
          </Border>
          <Border Grid.Column="2" Style="{StaticResource ChartCard}">
            <StackPanel>
              <Label Style="{StaticResource FSTrAkLabel}">Top Countries</Label>
              <lvc:PieChart x:Name="CountriesChart"
                            Series="{Binding CountriesSeries}"
                            MinHeight="200" IsClockwise="False"/>
            </StackPanel>
          </Border>
        </Grid>

      </StackPanel>
    </ScrollViewer>
  </UserControl>
  ```

- [ ] **Step 2: Add stat card and chart card styles to the app's resource dictionary**

  Open `FSTRaK/Resources/Styles.xaml` (or wherever `FSTrAkLabel` is defined — search for `x:Key="FSTrAkLabel"`). Add these styles:

  ```xml
  <Style x:Key="StatCard" TargetType="Border">
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="14,12"/>
    <Setter Property="MinWidth" Value="120"/>
  </Style>

  <Style x:Key="ChartCard" TargetType="Border">
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="14,12"/>
  </Style>

  <Style x:Key="StatCardLabel" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="FontSize" Value="{DynamicResource SmallFontSize}"/>
    <Setter Property="FontFamily" Value="{DynamicResource CurrentFont}"/>
    <Setter Property="TextWrapping" Value="Wrap"/>
  </Style>

  <Style x:Key="StatCardValue" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{DynamicResource TextColor}"/>
    <Setter Property="FontSize" Value="24"/>
    <Setter Property="FontWeight" Value="Bold"/>
    <Setter Property="FontFamily" Value="{DynamicResource CurrentFont}"/>
    <Setter Property="Margin" Value="0,4,0,0"/>
  </Style>

  <Style x:Key="StatCardUnit" TargetType="TextBlock">
    <Setter Property="Foreground" Value="{DynamicResource SubtleTextBrush}"/>
    <Setter Property="FontSize" Value="{DynamicResource SmallFontSize}"/>
    <Setter Property="FontFamily" Value="{DynamicResource CurrentFont}"/>
  </Style>
  ```

  Note: `CardBackgroundBrush`, `CardBorderBrush`, `AccentBrush`, `SubtleTextBrush` need to exist in both light and dark theme dictionaries. Check the existing theme files (look for `ResourceDictionary` files named `Dark.xaml`/`Light.xaml` or similar). Add these brush keys to each theme:
  - `CardBackgroundBrush` — slightly elevated surface (e.g. `#1A2A3A` dark / `#F5F7FA` light)
  - `CardBorderBrush` — subtle border (e.g. `#2A4A6A` dark / `#D0D8E4` light)
  - `AccentBrush` — existing accent color, or add `#64B5F6` dark / `#1565C0` light
  - `SubtleTextBrush` — muted text (e.g. `#8899AA` dark / `#6B7A8D` light)

- [ ] **Step 3: Build to check XAML compiles (binding errors are fine at this stage)**

- [ ] **Step 4: Commit**
  ```bash
  git add FSTRaK/Views/StatisticsView.xaml FSTRaK/Resources/
  git commit -m "feat: rebuild StatisticsView layout with stat cards and LiveCharts2 chart placeholders"
  ```

---

## Task 5: Wire up LiveCharts2 series bindings in ViewModel

**Files:**
- Modify: `FSTRaK/ViewModels/StatisticsViewModel.cs`

**Context:** LiveCharts2 uses bindable `ISeries[]` properties on the ViewModel (not code-behind rendering like ScottPlot). Add series and axis properties for each chart and update them in `UpdateStatisticsAsync`.

- [ ] **Step 1: Add `using` directives at top of `StatisticsViewModel.cs`**

  ```csharp
  using LiveChartsCore;
  using LiveChartsCore.SkiaSharpView;
  using LiveChartsCore.SkiaSharpView.Painting;
  using SkiaSharp;
  ```

- [ ] **Step 2: Add series properties for all charts**

  Add these properties after the existing distribution properties:

  ```csharp
  private ISeries[] _flightsPerPeriodSeries = Array.Empty<ISeries>();
  public ISeries[] FlightsPerPeriodSeries
  {
      get => _flightsPerPeriodSeries;
      set { _flightsPerPeriodSeries = value; OnPropertyChanged(); }
  }

  private Axis[] _flightsPerPeriodXAxes = Array.Empty<Axis>();
  public Axis[] FlightsPerPeriodXAxes
  {
      get => _flightsPerPeriodXAxes;
      set { _flightsPerPeriodXAxes = value; OnPropertyChanged(); }
  }

  private Axis[] _flightsPerPeriodYAxes = new[] { new Axis() };
  public Axis[] FlightsPerPeriodYAxes
  {
      get => _flightsPerPeriodYAxes;
      set { _flightsPerPeriodYAxes = value; OnPropertyChanged(); }
  }

  private ISeries[] _depAirportsSeries = Array.Empty<ISeries>();
  public ISeries[] DepAirportsSeries
  {
      get => _depAirportsSeries;
      set { _depAirportsSeries = value; OnPropertyChanged(); }
  }

  private ISeries[] _arrAirportsSeries = Array.Empty<ISeries>();
  public ISeries[] ArrAirportsSeries
  {
      get => _arrAirportsSeries;
      set { _arrAirportsSeries = value; OnPropertyChanged(); }
  }

  private ISeries[] _aircraftTypesSeries = Array.Empty<ISeries>();
  public ISeries[] AircraftTypesSeries
  {
      get => _aircraftTypesSeries;
      set { _aircraftTypesSeries = value; OnPropertyChanged(); }
  }

  private ISeries[] _airlinesSeries = Array.Empty<ISeries>();
  public ISeries[] AirlinesSeries
  {
      get => _airlinesSeries;
      set { _airlinesSeries = value; OnPropertyChanged(); }
  }

  private ISeries[] _landingRateSeries = Array.Empty<ISeries>();
  public ISeries[] LandingRateSeries
  {
      get => _landingRateSeries;
      set { _landingRateSeries = value; OnPropertyChanged(); }
  }

  private Axis[] _landingRateXAxes = Array.Empty<Axis>();
  public Axis[] LandingRateXAxes
  {
      get => _landingRateXAxes;
      set { _landingRateXAxes = value; OnPropertyChanged(); }
  }

  private Axis[] _landingRateYAxes = new[] { new Axis() };
  public Axis[] LandingRateYAxes
  {
      get => _landingRateYAxes;
      set { _landingRateYAxes = value; OnPropertyChanged(); }
  }

  private ISeries[] _countriesSeries = Array.Empty<ISeries>();
  public ISeries[] CountriesSeries
  {
      get => _countriesSeries;
      set { _countriesSeries = value; OnPropertyChanged(); }
  }
  ```

- [ ] **Step 3: Add `BuildPieSeries` helper method**

  ```csharp
  private static ISeries[] BuildPieSeries(Dictionary<string, double> data)
  {
      var palette = new[]
      {
          SKColor.Parse("#4FC3F7"), SKColor.Parse("#81C784"), SKColor.Parse("#FFB74D"),
          SKColor.Parse("#F06292"), SKColor.Parse("#CE93D8"), SKColor.Parse("#80DEEA")
      };
      return data.Select((kv, i) => (ISeries)new PieSeries<double>
      {
          Name = kv.Key,
          Values = new[] { kv.Value },
          Fill = new SolidColorPaint(palette[i % palette.Length])
      }).ToArray();
  }
  ```

- [ ] **Step 4: Add `BuildFlightsPerPeriod` helper method**

  ```csharp
  private static (ISeries[] series, Axis[] xAxes) BuildFlightsPerPeriod(
      Dictionary<DateTime, double> flightsPerDay, TimePeriod period)
  {
      Dictionary<DateTime, double> data;
      string labelFormat;

      if (period == TimePeriod.Month)
      {
          data = flightsPerDay
              .GroupBy(x => new DateTime(x.Key.Year, x.Key.Month, 1))
              .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
          labelFormat = "MMM yyyy";
      }
      else if (period == TimePeriod.Year)
      {
          data = flightsPerDay
              .GroupBy(x => new DateTime(x.Key.Year, 1, 1))
              .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
          labelFormat = "yyyy";
      }
      else
      {
          data = flightsPerDay;
          labelFormat = "dd/MM/yy";
      }

      var ordered = data.OrderBy(kv => kv.Key).ToList();
      var values = ordered.Select(kv => kv.Value).ToArray();
      var labels = ordered.Select(kv => kv.Key.ToString(labelFormat)).ToArray();

      var series = new ISeries[]
      {
          new ColumnSeries<double>
          {
              Values = values,
              Fill = new SolidColorPaint(SKColor.Parse("#4FC3F7")),
              Name = "Flights"
          }
      };

      var xAxes = new[]
      {
          new Axis
          {
              Labels = labels,
              LabelsRotation = -45,
              TextSize = 10
          }
      };

      return (series, xAxes);
  }
  ```

- [ ] **Step 5: Add `BuildLandingRateSeries` helper method**

  ```csharp
  private static (ISeries[] series, Axis[] xAxes) BuildLandingRateSeries(
      List<(double bucketCenter, int count)> dist)
  {
      var values = dist.Select(d => (double)d.count).ToArray();
      var labels = dist.Select(d => $"{(int)d.bucketCenter}").ToArray();

      var series = new ISeries[]
      {
          new ColumnSeries<double>
          {
              Values = values,
              Fill = new SolidColorPaint(SKColor.Parse("#81C784")),
              Name = "Landings"
          }
      };

      var xAxes = new[]
      {
          new Axis
          {
              Labels = labels,
              LabelsRotation = -45,
              TextSize = 10,
              Name = "fpm"
          }
      };

      return (series, xAxes);
  }
  ```

- [ ] **Step 6: Call the builders in `UpdateStatisticsAsync` dispatcher block**

  Inside `App.Current.Dispatcher.Invoke`, after setting `FlightRoutes = flightRoutes;`, add:

  ```csharp
  var (fpSeries, fpXAxes) = BuildFlightsPerPeriod(flightsPerDay, TimePeriod);
  FlightsPerPeriodSeries = fpSeries;
  FlightsPerPeriodXAxes = fpXAxes;

  DepAirportsSeries = BuildPieSeries(depDist);
  ArrAirportsSeries = BuildPieSeries(arrDist);
  AircraftTypesSeries = BuildPieSeries(aircraftDist);
  AirlinesSeries = BuildPieSeries(airlineDist);
  CountriesSeries = BuildPieSeries(countryDist);

  var (lrSeries, lrXAxes) = BuildLandingRateSeries(landingDist);
  LandingRateSeries = lrSeries;
  LandingRateXAxes = lrXAxes;
  ```

- [ ] **Step 7: Rebuild charts when `TimePeriod` changes**

  In the `TimePeriod` property setter, after `OnPropertyChanged()`, add:

  ```csharp
  if (FlightsPerDay != null && FlightsPerDay.Any())
  {
      var (fpSeries, fpXAxes) = BuildFlightsPerPeriod(FlightsPerDay, _timePeriod);
      App.Current.Dispatcher.Invoke(() =>
      {
          FlightsPerPeriodSeries = fpSeries;
          FlightsPerPeriodXAxes = fpXAxes;
      });
  }
  ```

- [ ] **Step 8: In the empty-results branch of `UpdateStatisticsAsync`, clear all series**

  Add after existing empty-result assignments:

  ```csharp
  FlightsPerPeriodSeries = Array.Empty<ISeries>();
  FlightsPerPeriodXAxes = Array.Empty<Axis>();
  DepAirportsSeries = Array.Empty<ISeries>();
  ArrAirportsSeries = Array.Empty<ISeries>();
  AircraftTypesSeries = Array.Empty<ISeries>();
  AirlinesSeries = Array.Empty<ISeries>();
  LandingRateSeries = Array.Empty<ISeries>();
  LandingRateXAxes = Array.Empty<Axis>();
  CountriesSeries = Array.Empty<ISeries>();
  ```

- [ ] **Step 9: Build and verify no compile errors**

- [ ] **Step 10: Commit**
  ```bash
  git add FSTRaK/ViewModels/StatisticsViewModel.cs
  git commit -m "feat: add LiveCharts2 series bindings to StatisticsViewModel"
  ```

---

## Task 6: Rebuild StatisticsView.xaml.cs — remove ScottPlot, add map rendering

**Files:**
- Modify: `FSTRaK/Views/StatisticsView.xaml.cs`

**Context:** The current code-behind is entirely ScottPlot rendering (pie charts, histogram). Replace it with: `OnLoaded` wiring, map layer setup (same as `LiveView.xaml.cs`), and route polyline rendering when `FlightRoutes` changes.

- [ ] **Step 1: Replace `StatisticsView.xaml.cs` entirely**

  ```csharp
  using System.Windows;
  using System.Windows.Controls;
  using FSTRaK.ViewModels;
  using FSTRaK.Utils;
  using MapControl;

  namespace FSTRaK.Views
  {
      public partial class StatisticsView : UserControl
      {
          private MapTileLayerBase _currentOpenAipLayer;
          private MapTileLayerBase _currentChartLayer;

          public StatisticsView()
          {
              InitializeComponent();
          }

          private void OnLoaded(object sender, RoutedEventArgs e)
          {
              MapLayerHelper.UpdateMapLayers(RouteMap, ref _currentOpenAipLayer, ref _currentChartLayer);

              var vm = (StatisticsViewModel)DataContext;
              vm.PropertyChanged += (s, args) =>
              {
                  if (args.PropertyName == nameof(StatisticsViewModel.FlightRoutes))
                  {
                      RenderRoutePolylines();
                  }
              };

              vm.ViewLoaded();
          }

          private void RenderRoutePolylines()
          {
              var vm = (StatisticsViewModel)DataContext;
              if (vm?.FlightRoutes == null) return;

              // Remove existing route polylines (leave map layers/children that are not MapPolyline)
              var toRemove = RouteMap.Children
                  .OfType<MapPolyline>()
                  .ToList();
              foreach (var line in toRemove)
                  RouteMap.Children.Remove(line);

              foreach (var (dep, arr) in vm.FlightRoutes)
              {
                  var polyline = new MapPolyline
                  {
                      Locations = new LocationCollection { dep, arr },
                      Stroke = (System.Windows.Media.Brush)FindResource("FlightPathColorBrush"),
                      StrokeThickness = 1,
                      Opacity = 0.5
                  };
                  RouteMap.Children.Add(polyline);
              }
          }
      }
  }
  ```

  Note: `LocationCollection` requires `using MapControl;`. The `using System.Linq;` is needed for `.OfType<>()`.

- [ ] **Step 2: Add missing `using` directives**

  At the top of the file, ensure:
  ```csharp
  using System.Linq;
  using System.Windows;
  using System.Windows.Controls;
  using FSTRaK.ViewModels;
  using FSTRaK.Utils;
  using MapControl;
  ```

- [ ] **Step 3: Build and verify no compile errors**

- [ ] **Step 4: Commit**
  ```bash
  git add FSTRaK/Views/StatisticsView.xaml.cs
  git commit -m "feat: replace ScottPlot code-behind with LiveCharts2 bindings and route map rendering"
  ```

---

## Task 7: Remove unused ScottPlot references from StatisticsView

**Files:**
- Modify: `FSTRaK/Views/StatisticsView.xaml.cs` (already done in Task 6)
- Check: `FSTRaK/FSTrAk.csproj` — ScottPlot may still be used by other views (FlightDetailsView), do NOT remove the NuGet package globally

- [ ] **Step 1: Verify ScottPlot is still used by other views**
  ```bash
  grep -rn "ScottPlot\|WpfPlot" FSTRaK/Views/ --include="*.cs" --include="*.xaml"
  ```
  Expected: results in `FlightDetailsView.xaml.cs` and/or `FlightDetailsView.xaml` but NOT in `StatisticsView.*`

- [ ] **Step 2: Build and run**

  Build `x64|Debug`. Launch the app. Navigate to the Statistics page. Verify:
  - Stat cards appear
  - Charts render (LiveCharts2)
  - Map appears with the current map provider
  - Filters work and narrow each other
  - No ScottPlot artifacts

- [ ] **Step 3: Commit if any cleanup was needed**
  ```bash
  git commit -am "chore: clean up remaining ScottPlot references in Statistics"
  ```

---

## Task 8: Final polish and branch ready

- [ ] **Step 1: Verify all 7 stat cards display correct values**

  Navigate to Statistics. Check each card matches data in the Logbook view.

- [ ] **Step 2: Verify filter interdependency**

  Select an airline in the Airline filter. Confirm that Aircraft Type and Tail Number dropdowns now only show options for that airline.

- [ ] **Step 3: Verify route map**

  Confirm geodesic lines appear for flights. Zoom and pan work.

- [ ] **Step 4: Verify landing rate histogram**

  Confirm histogram bars are visible and x-axis shows fpm buckets.

- [ ] **Step 5: Verify weight units**

  Change units in Settings (imperial ↔ metric). Return to Statistics. Confirm Fuel Used and Payload cards update units accordingly.

- [ ] **Step 6: Final commit**
  ```bash
  git add -A
  git commit -m "feat: statistics page overhaul — dashboard cards, route map, LiveCharts2, interdependent filters"
  ```

---

## Self-Review Notes

- All 7 stat card values map to existing ViewModel properties (no new ones needed)
- `FlightRoutes` uses `MapControl.Location` — same type as `FlightDetailsViewModel.FlightPath`
- `BuildPieSeries` uses 6-color palette matching `ChartColor1`–`ChartColor6` spirit
- `TimePeriod` change triggers chart rebuild — old `FlightsPerDay` and `TimePeriod` case in code-behind is fully removed
- `CreateFiltersAsync` cache DTO now includes `TailNumbers` — old cache files with 2-field DTOs will deserialize with `TailNumbers == null` and fall through to a full DB query, which is safe
- ScottPlot is preserved for `FlightDetailsView` — only removed from Statistics

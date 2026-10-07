using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FSTRaK.ViewModels;
using FSTRaK.Utils;
using MapControl;

namespace FSTRaK.Views
{
    public partial class StatisticsView : UserControl
    {
        /// <summary>
        /// Coarser than the flight-plan overlay's spacing: the statistics map can draw every
        /// route in the logbook at once, and at this zoom the extra points would not be
        /// visible anyway.
        /// </summary>
        private const double RouteStepNm = 200.0;

        private MapTileLayerBase _currentAeroOverlayLayer;
        private MapTileLayerBase _currentChartLayer;

        public StatisticsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MapLayerHelper.UpdateMapLayers(RouteMap, ref _currentAeroOverlayLayer, ref _currentChartLayer);

            // Catch the bubble phase of MouseWheel on the map and stop it there,
            // so it never reaches the ScrollViewer. MapControl zooms via PreviewMouseWheel
            // (tunnel phase) which has already fired by the time we see the bubble.
            RouteMap.AddHandler(
                MouseWheelEvent,
                new MouseWheelEventHandler(OnMapMouseWheel),
                handledEventsToo: false);

            var vm = (StatisticsViewModel)DataContext;
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(StatisticsViewModel.FlightRoutes))
                    RenderRoutePolylines();
            };

            vm.ViewLoaded();
        }

        // Fires during bubble phase after MapControl has already handled zooming.
        // Mark handled so the event doesn't bubble up to the ScrollViewer.
        private void OnMapMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
        }

        private void RenderRoutePolylines()
        {
            var vm = (StatisticsViewModel)DataContext;
            if (vm?.FlightRoutes == null) return;

            var toRemove = RouteMap.Children.OfType<MapPolyline>().ToList();
            foreach (var line in toRemove)
                RouteMap.Children.Remove(line);

            var stroke = (Brush)TryFindResource("FlightPathColorBrush")
                         ?? Brushes.OrangeRed;

            foreach (var route in vm.FlightRoutes)
            {
                var locations = new LocationCollection();
                var greatCircle = GeodesicUtil.Interpolate(
                    route.Departure.Latitude, route.Departure.Longitude,
                    route.Arrival.Latitude, route.Arrival.Longitude,
                    RouteStepNm);

                // Unwrapped so a trans-dateline route draws across the Pacific rather than
                // back across the whole map.
                foreach (var pt in MapUtils.WrapPolyline(greatCircle))
                    locations.Add(pt);

                RouteMap.Children.Add(new MapPolyline
                {
                    Locations = locations,
                    Stroke = stroke,
                    StrokeThickness = 1,
                    Opacity = 0.5,
                    ToolTip = BuildRouteTooltip(route)
                });

                // A one-pixel line at half opacity is not something anyone can hover, so the
                // route carries a transparent companion purely as a hit target. Transparent
                // is deliberate - a null brush is not hit-testable in WPF, so it would give
                // back nothing. A second, separate tooltip panel is built here because a
                // FrameworkElement can only have one visual parent - the same instance can't
                // be reused as both polylines' ToolTip.
                RouteMap.Children.Add(new MapPolyline
                {
                    Locations = locations,
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 10,
                    ToolTip = BuildRouteTooltip(route)
                });
            }
        }

        /// <summary>
        /// A route may carry several flights (both directions between the same two airports
        /// are merged into one line), so the tooltip lists each one rather than showing a
        /// single flight's detail.
        /// </summary>
        private static FrameworkElement BuildRouteTooltip(FlightRouteLine route)
        {
            var panel = new StackPanel();
            foreach (var f in route.Flights)
            {
                var line = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
                line.Children.Add(new TextBlock { Text = f.Direction, FontWeight = FontWeights.Bold });

                var detailParts = new[] { f.Date, f.Airline, f.Aircraft }
                    .Where(part => !string.IsNullOrWhiteSpace(part));
                line.Children.Add(new TextBlock { Text = string.Join(" · ", detailParts) });

                panel.Children.Add(line);
            }
            return panel;
        }
    }
}

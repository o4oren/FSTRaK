using System;
using System.Collections.Generic;
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
                foreach (var pt in GetGeodesicPoints(route.Departure, route.Arrival))
                    locations.Add(pt);

                RouteMap.Children.Add(new MapPolyline
                {
                    Locations = locations,
                    Stroke = stroke,
                    StrokeThickness = 1,
                    Opacity = 0.5,
                    ToolTip = route.TooltipText
                });

                // A one-pixel line at half opacity is not something anyone can hover, so the
                // route carries a transparent companion purely as a hit target. Transparent
                // is deliberate - a null brush is not hit-testable in WPF, so it would give
                // back nothing.
                RouteMap.Children.Add(new MapPolyline
                {
                    Locations = locations,
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 10,
                    ToolTip = route.TooltipText
                });
            }
        }

        private static IEnumerable<Location> GetGeodesicPoints(Location from, Location to, int steps = 20)
        {
            double lat1 = from.Latitude * Math.PI / 180;
            double lon1 = from.Longitude * Math.PI / 180;
            double lat2 = to.Latitude * Math.PI / 180;
            double lon2 = to.Longitude * Math.PI / 180;

            double d = 2 * Math.Asin(Math.Sqrt(
                Math.Pow(Math.Sin((lat2 - lat1) / 2), 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin((lon2 - lon1) / 2), 2)));

            if (d < 0.001) { yield return from; yield return to; yield break; }

            for (int i = 0; i <= steps; i++)
            {
                double f = (double)i / steps;
                double A = Math.Sin((1 - f) * d) / Math.Sin(d);
                double B = Math.Sin(f * d) / Math.Sin(d);
                double x = A * Math.Cos(lat1) * Math.Cos(lon1) + B * Math.Cos(lat2) * Math.Cos(lon2);
                double y = A * Math.Cos(lat1) * Math.Sin(lon1) + B * Math.Cos(lat2) * Math.Sin(lon2);
                double z = A * Math.Sin(lat1) + B * Math.Sin(lat2);
                double lat = Math.Atan2(z, Math.Sqrt(x * x + y * y)) * 180 / Math.PI;
                double lon = Math.Atan2(y, x) * 180 / Math.PI;
                yield return new Location(lat, lon);
            }
        }
    }
}

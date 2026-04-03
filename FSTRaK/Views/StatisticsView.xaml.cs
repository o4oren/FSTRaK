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
        private MapTileLayerBase _currentOpenAipLayer;
        private MapTileLayerBase _currentChartLayer;

        public StatisticsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MapLayerHelper.UpdateMapLayers(RouteMap, ref _currentOpenAipLayer, ref _currentChartLayer);

            // Intercept PreviewMouseWheel on the map before it reaches the ScrollViewer.
            // We let MapControl zoom normally, then stop the event bubbling up.
            RouteMap.AddHandler(
                PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnMapPreviewMouseWheel),
                handledEventsToo: false);

            var vm = (StatisticsViewModel)DataContext;
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(StatisticsViewModel.FlightRoutes))
                    RenderRoutePolylines();
            };

            vm.ViewLoaded();
        }

        // Called during the tunnel phase on the map — before the ScrollViewer sees it.
        // MapControl's internal PreviewMouseWheel zoom handler fires at the same phase
        // on the map element, so zoom still works. We then mark it handled so the
        // ScrollViewer's PreviewMouseWheel handler (which scrolls the page) never fires.
        private void OnMapPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        // Kept for the ScrollViewer handler wired in XAML — no-op since the map handler
        // marks events handled before they reach here when mouse is over the map.
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

            foreach (var (dep, arr) in vm.FlightRoutes)
            {
                var locations = new LocationCollection();
                foreach (var pt in GetGeodesicPoints(dep, arr))
                    locations.Add(pt);

                RouteMap.Children.Add(new MapPolyline
                {
                    Locations = locations,
                    Stroke = stroke,
                    StrokeThickness = 1,
                    Opacity = 0.5
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

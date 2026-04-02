using System.Linq;
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

            // Remove existing route polylines
            var toRemove = RouteMap.Children.OfType<MapPolyline>().ToList();
            foreach (var line in toRemove)
                RouteMap.Children.Remove(line);

            var stroke = (System.Windows.Media.Brush)TryFindResource("FlightPathColorBrush")
                         ?? System.Windows.Media.Brushes.OrangeRed;

            foreach (var (dep, arr) in vm.FlightRoutes)
            {
                var polyline = new MapPolyline
                {
                    Locations = new LocationCollection { dep, arr },
                    Stroke = stroke,
                    StrokeThickness = 1,
                    Opacity = 0.5
                };
                RouteMap.Children.Add(polyline);
            }
        }
    }
}

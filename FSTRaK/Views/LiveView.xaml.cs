using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using FSTRaK.BusinessLogic.VatsimService;
using FSTRaK.DataTypes;
using FSTRaK.Utils;
using MapControl;


namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for FlightData.xaml
    /// </summary>
    public partial class LiveView : System.Windows.Controls.UserControl
    {
        private MapTileLayerBase _currentAeroOverlayLayer;
        private MapTileLayerBase _currentChartLayer;
        private System.Windows.Point _mouseDownPosition;
        private const double DragThreshold = 5.0; // pixels

        public LiveView()
        {
            InitializeComponent();
            Unloaded += OnUnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs re)
        {
            SetAirplaneGeometry(((LiveViewViewModel)DataContext).AirplaneIcon);

            Properties.Settings.Default.PropertyChanged += OnSettingsPropertyChanged;
            UpdateMapLayers();
            ((LiveViewViewModel)DataContext).NotifyMapProviderChanged();

            ((LiveViewViewModel)DataContext).PropertyChanged += OnViewModelPropertyChanged;
            KeyDown += OnKeyDown;
            xMap.MouseLeftButtonDown += OnMapMouseDown;
            xMap.MouseLeftButtonUp += OnMapMouseUp;
        }

        private void OnUnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LiveViewViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;

            Properties.Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;
            KeyDown -= OnKeyDown;
            if (_currentAeroOverlayLayer != null)
            {
                xMap.Children.Remove(_currentAeroOverlayLayer);
                _currentAeroOverlayLayer = null;
            }
            if (_currentChartLayer != null)
            {
                xMap.Children.Remove(_currentChartLayer);
                _currentChartLayer = null;
            }
            xMap.MouseLeftButtonDown -= OnMapMouseDown;
            xMap.MouseLeftButtonUp -= OnMapMouseUp;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataContext == null) return;

            switch (e.PropertyName)
            {
                case "AirplaneIcon":
                    SetAirplaneGeometry(((LiveViewViewModel)DataContext).AirplaneIcon);
                    break;
                case "MapProvider":
                    UpdateMapLayers();
                    break;
            }
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "MapTileProvider" ||
                e.PropertyName == "ChartOverlayProvider" ||
                e.PropertyName == "IsOpenAipEnabled" ||
                e.PropertyName == "IsOpenFlightMapsEnabled")
            {
                var vm = DataContext as LiveViewViewModel;
                vm?.NotifyMapProviderChanged();
            }
        }

        private void SetAirplaneGeometry(string iconKey)
        {
            if (string.IsNullOrEmpty(iconKey)) return;
            var geometry = Application.Current.Resources[iconKey] as System.Windows.Media.Geometry;
            if (geometry != null)
                AirplaneGeometry.Data = geometry;
        }

        private void UpdateMapLayers()
        {
            MapLayerHelper.UpdateMapLayers(xMap, ref _currentAeroOverlayLayer, ref _currentChartLayer);
        }

        private void OnMapMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _mouseDownPosition = e.GetPosition(xMap);
        }

        private void OnMapMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(xMap);
            var dx = pos.X - _mouseDownPosition.X;
            var dy = pos.Y - _mouseDownPosition.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
            {
                var vm = DataContext as LiveViewViewModel;
                ((System.Windows.Input.ICommand)vm?.ClearSelectionCommand)?.Execute(null);
            }
        }

        private void OnMapItemClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext != null)
            {
                var vm = DataContext as FSTRaK.ViewModels.LiveViewViewModel;
                ((System.Windows.Input.ICommand)vm?.SelectClientCommand)?.Execute(fe.DataContext);
                e.Handled = true;
            }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                var vm = DataContext as FSTRaK.ViewModels.LiveViewViewModel;
                ((System.Windows.Input.ICommand)vm?.ClearSelectionCommand)?.Execute(null);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}

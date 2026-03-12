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
        private MapTileLayerBase _currentOverlayLayer;

        public LiveView()
        {
            InitializeComponent();
            Unloaded += OnUnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs re)
        {
            var geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
            AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;

            ((LiveViewViewModel)DataContext).PropertyChanged += OnViewModelPropertyChanged;

            Properties.Settings.Default.PropertyChanged += OnSettingsPropertyChanged;
            UpdateMapLayers();
        }

        private void OnUnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LiveViewViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;

            Properties.Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;
            if (_currentOverlayLayer != null)
            {
                xMap.Children.Remove(_currentOverlayLayer);
                _currentOverlayLayer = null;
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DataContext == null) return;

            switch (e.PropertyName)
            {
                case "AirplaneIcon":
                    var geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
                    AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;
                    break;
                case "MapProvider":
                    UpdateMapLayers();
                    break;
            }
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "MapTileProvider")
            {
                var vm = DataContext as LiveViewViewModel;
                vm?.NotifyMapProviderChanged();
            }
        }

        private void UpdateMapLayers()
        {
            var vm = DataContext as LiveViewViewModel;
            var provider = vm?.MapProvider;
            if (provider == null) return;

            if (_currentOverlayLayer != null)
            {
                xMap.Children.Remove(_currentOverlayLayer);
                _currentOverlayLayer = null;
            }

            if (provider is IOverlayMapTileLayer)
            {
                var osmBase = Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
                xMap.MapLayer = osmBase;
                var baseIndex = xMap.Children.IndexOf(osmBase);
                xMap.Children.Insert(baseIndex + 1, provider);
                _currentOverlayLayer = provider;
            }
            else
            {
                xMap.MapLayer = provider;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}

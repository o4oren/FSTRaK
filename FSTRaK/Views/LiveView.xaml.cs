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
        private MapTileLayerBase _currentOpenAipLayer;
        private MapTileLayerBase _currentChartLayer;

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
        }

        private void OnUnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LiveViewViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;

            Properties.Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;
            KeyDown -= OnKeyDown;
            if (_currentOpenAipLayer != null)
            {
                xMap.Children.Remove(_currentOpenAipLayer);
                _currentOpenAipLayer = null;
            }
            if (_currentChartLayer != null)
            {
                xMap.Children.Remove(_currentChartLayer);
                _currentChartLayer = null;
            }
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
                e.PropertyName == "IsOpenAipEnabled")
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
            MapLayerHelper.UpdateMapLayers(xMap, ref _currentOpenAipLayer, ref _currentChartLayer);
        }

        private void OnMapItemClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext != null)
            {
                var vm = DataContext as FSTRaK.ViewModels.LiveViewViewModel;
                vm?.SelectClientCommand.Execute(fe.DataContext);
                e.Handled = true;
            }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                var vm = DataContext as FSTRaK.ViewModels.LiveViewViewModel;
                vm?.ClearSelectionCommand.Execute(null);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}

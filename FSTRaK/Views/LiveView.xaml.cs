using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MapControl;
using System;
using System.Windows.Media.Imaging;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using Microsoft.VisualBasic.Logging;
using FSTRaK.Utils;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for FlightData.xaml
    /// </summary>
    public partial class LiveView : System.Windows.Controls.UserControl
    {
        public LiveView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs re)
        {
            var geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
            AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;
 
            ((LiveViewViewModel)DataContext).PropertyChanged += ((o, e) =>
            {
                if(DataContext != null)
                {
                    var liveViewViewModel = (LiveViewViewModel)DataContext;
                    if (e.PropertyName == "AirplaneIcon")
                    {
                        geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
                        AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;
                    }

                    else if (e.PropertyName == "VatsimData")
                    {
                        if (liveViewViewModel.IsShowVatsimAircraft)
                        {
                            DrawPilots();
                        }
                        if (liveViewViewModel.IsShowVatsimAirports)
                        {
                            DrawAirports(liveViewViewModel);
                        }
                    }
                }
            });
        }

        private void DrawAirports(LiveViewViewModel liveViewViewModel)
        {
            vatsimAirportsOverlay.Children.Clear();
            foreach (var ca in liveViewViewModel.ControlledAirports)
            {
                var controlledAirport = ca.Value;
                var image = new Image()
                {
                    Height = 32,
                    Width = 32, 
                    Source = new BitmapImage(new Uri($@"pack://application:,,,/Resources/Images/control-tower.png",
                        UriKind.Absolute))
                };


                MapItem mi = new MapItem
                {
                    Location = new Location(controlledAirport.Airport.Latitude, controlledAirport.Airport.Longitude),
                    Content = image,
                    Margin = new Thickness(-16, -16, 0, 0)
                };
                ToolTip toolTip = new ToolTip();
                toolTip.Content = $"{controlledAirport.Controllers[0].name}\n{controlledAirport.Controllers[0].callsign}";
                mi.ToolTip = toolTip;
                
                vatsimAirportsOverlay.Children.Add(mi);
            }
        }

        private void DrawPilots()
        {
            vatsimAircraftOverlay.Children.Clear();
            foreach (var pilot in ((LiveViewViewModel)DataContext).VatsimData.pilots)
            {

                (string aircraftIcon, double scaleFactor) = pilot.flight_plan != null ? ResourceUtils.GetAircraftIcon(pilot.flight_plan.aircraft_short) : ("B737", 0.75);

                Path path = new Path
                {
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 1,
                    Fill = (Brush)mainViewResources["PrimaryDarkBrush"],
                    Data = (Geometry)mainViewResources[aircraftIcon]
                };

                MapItem mi = new MapItem
                {
                    Location = new Location(pilot.latitude, pilot.longitude),
                    Content = path,
                    Margin = new Thickness(-16 * scaleFactor, -16 * scaleFactor, 0, 0)
                };

                var rotateTransform = new RotateTransform(pilot.heading, 16, 16);
                var scaleTransform = new ScaleTransform(1 * scaleFactor, 1 * scaleFactor);
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(rotateTransform);
                transformGroup.Children.Add(scaleTransform);
                mi.RenderTransform = transformGroup;
                
                ToolTip toolTip = new ToolTip
                {
                    Content = $"{pilot.callsign} {pilot.name}"
                };
                if (pilot.flight_plan != null)
                {
                    toolTip.Content = (string)toolTip.Content + "\n" + pilot.flight_plan.aircraft_short;
                }
                mi.ToolTip = toolTip;
                vatsimAircraftOverlay.Children.Add(mi);
            }
        }
    }
}

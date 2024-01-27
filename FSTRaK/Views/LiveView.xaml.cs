using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using Microsoft.VisualBasic.Logging;
using FSTRaK.Utils;
using MapControl;

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
                bool isIncludeApp = false;
                var controlledAirport = ca.Value;
                var image = new Image()
                {
                    Height = 32,
                    Width = 32, 
                    Source = new BitmapImage(new Uri($@"pack://application:,,,/Resources/Images/control-tower.png",
                        UriKind.Absolute))
                };

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{controlledAirport.Airport.ICAO} {controlledAirport.Airport.Name} ");
                foreach (var controller in controlledAirport.Controllers)
                {
                    sb.AppendLine($"{controller.callsign} {controller.name} {controller.frequency} {controller.facility}");
                    if (controller.facility == 5)
                    {
                        isIncludeApp = true;
                    }
                }


                if (isIncludeApp)
                {
                    MapPolygon circlePolygon = new MapPolygon
                    {
                        Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                        Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink),
                        StrokeThickness = 1,
                        Opacity = 0.3
                    };

                    // Calculate vertices for a circle
                    int numberOfVertices = 80; // Adjust as needed for smoothness
                    double radius = 80; 
                    List<Location> locations = new List<Location>();
                    for (int i = 0; i < numberOfVertices; i++)
                    {
                        double angle = (i * 2 * Math.PI) / numberOfVertices;
                        double latitude = controlledAirport.Airport.Latitude + (radius / 111.32) * Math.Sin(angle); // 1 degree of latitude is approximately 111.32 km
                        double longitude = controlledAirport.Airport.Longitude + (radius / (111.32 * Math.Cos(47.6097 * (Math.PI / 180)))) * Math.Cos(angle);
                        locations.Add(new Location(latitude, longitude));
                       
                    }
                    circlePolygon.Locations = locations;

                    vatsimAirportsOverlay.Children.Add(circlePolygon);
                }
                
                MapItem mi = new MapItem
                {
                    Location = new Location(controlledAirport.Airport.Latitude, controlledAirport.Airport.Longitude),
                    Content = image,
                    Margin = new Thickness(-16, -16, 0, 0)
                };
                ToolTip toolTip = new ToolTip();
                toolTip.Content = sb.ToString();
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

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{pilot.callsign} {pilot.name}");
                if (pilot.flight_plan != null)
                {
                    sb.AppendLine($"Flying from {pilot.flight_plan.departure} to {pilot.flight_plan.arrival}");
                    sb.AppendLine($"Flying from {pilot.flight_plan.aircraft_short}  {pilot.flight_plan.aircraft}");
                }
                sb.AppendLine($"Altitude: {pilot.altitude} ft");
                sb.AppendLine($"Heading: {pilot.heading}");
                sb.AppendLine($"Ground Speed: {pilot.groundspeed} Kts");

                if (pilot.flight_plan != null)
                {
                    sb.AppendLine($"Flight Plan:\n {pilot.flight_plan.route}");
                    sb.AppendLine($"Remarks:\n {pilot.flight_plan.remarks}");
                }

                ToolTip toolTip = new ToolTip
                {
                    Content = sb.ToString()
                };

                mi.ToolTip = toolTip;
                vatsimAircraftOverlay.Children.Add(mi);
            }
        }
    }
}

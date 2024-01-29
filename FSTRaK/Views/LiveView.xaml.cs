using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using System.Text;
using System.Windows.Media.Imaging;
using FSTRaK.BusinessLogic.VatsimService;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.DataTypes;
using Microsoft.VisualBasic.Logging;
using FSTRaK.Utils;
using MapControl;
using ScottPlot.Drawing.Colormaps;
using ScottPlot.Renderable;

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

                    switch (e.PropertyName)
                    {
                        case "AirplaneIcon":
                            geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
                            AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;
                            break;
                        case "VatsimData":
                            if (liveViewViewModel.IsShowVatsimAircraft)
                            {
                                DrawPilots();
                            }


                            if (liveViewViewModel.IsShowVatsimFirs)
                            {
                                DrawFirs(liveViewViewModel);
                            }
                            break;
                        case "ControlledAirports":
                            if (liveViewViewModel.IsShowVatsimAirports)
                            {
                                DrawAirports(liveViewViewModel);
                            }

                            break;
                        case "IsShowVatsimAirports":
                            if (!liveViewViewModel.IsShowVatsimAirports)
                            {
                                vatsimAirportsOverlay.Children.Clear();
                                vatsimAppCirclesOverlay.Children.Clear();
                            }

                            break;
                        case "IsShowVatsimAircraft":
                            if(!liveViewViewModel.IsShowVatsimAircraft)
                                vatsimAircraftOverlay.Children.Clear();
                            break;
                        case "IsShowVatsimFirs":
                            if (!liveViewViewModel.IsShowVatsimFirs)
                            {
                                vatsimFIRsTextOverlay.Children.Clear();
                                vatsimFIRsOverlay.Children.Clear();
                                vatsimUIRsOverlay.Children.Clear();
                            }
                            break;
                        default:
                            break;

                    }
                }
            });
        }

        private void DrawFirs(LiveViewViewModel liveViewViewModel)
        {
            vatsimFIRsOverlay.Children.Clear();
            vatsimFIRsTextOverlay.Children.Clear();
            foreach (var controller in liveViewViewModel.VatsimData.controllers)
            {
                if (controller.facility == 6 || controller.facility == 1)
                {
                    try
                    {

                        if (controller.frequency.Equals("199.998"))
                        {
                            continue;
                        }

                        if (controller.callsign.Equals("ZAK_FSS"))
                        {
                            ;
                        }

                        var firs = VatsimService.Instance.GetUirBoundariesByController(controller);

                        if (firs.Count == 0)
                        {
                            var firMetadataTuple = VatsimService.Instance.GetFirBoundariesByController(controller);
                            firs.Add(firMetadataTuple);
                        }

                        int i = 0;
                        List<LocationCollection> uirLocations = new List<LocationCollection>();
                        foreach (var firMetadataTuple in firs)
                        {
                            
                            List<LocationCollection> locations = new List<LocationCollection>();

                            foreach (var geoJsonCoordinate in firMetadataTuple.coordinates)
                            {
                                {
                                    LocationCollection locationCollection = new LocationCollection();
                                    foreach (var coords in geoJsonCoordinate[0])
                                    {
                                        locationCollection.Add(new Location(coords[1], coords[0]));
                                    }
                                    locations.Add(locationCollection);
                                    if (firs.Count > 1)
                                    {
                                        uirLocations.Add(locationCollection);
                                    }
                                    
                                }
                            }


                            StackPanel stackPanel = null;
                            foreach (UIElement child in vatsimFIRsTextOverlay.Children)
                            {
                                if (child is MapItem)
                                {
                                    var mapItem = child as MapItem;
                                    if (mapItem.Location.Latitude == firMetadataTuple.labelCoordinates[0]
                                        && mapItem.Location.Longitude == firMetadataTuple.labelCoordinates[1])
                                    {
                                        stackPanel = mapItem.Content as StackPanel;
                                    }
                                }
                            }

                            stackPanel ??= new StackPanel
                            {
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            };


                            var strokeBrush = firs.Count > 1 ? (Brush)mainViewResources["VatsimUirStrokeBrush"] : (Brush)mainViewResources["VatsimFirStrokeBrush"];
                            var fillBrush = firs.Count > 1 ?  (Brush)mainViewResources["VatsimUirFillBrush"] : (Brush)mainViewResources["VatsimFirFillBrush"];

                            foreach (var locationCollection in locations)
                            {
                                MapPolygon polygon = new MapPolygon
                                {
                                    Stroke = strokeBrush,
                                    Fill = fillBrush,
                                    StrokeThickness = 2,
                                    Locations = locationCollection
                                };
                                if (firs.Count > 0)
                                {
                                    vatsimUIRsOverlay.Children.Add(polygon);
                                }
                                else
                                {
                                    vatsimFIRsOverlay.Children.Add(polygon);
                                }
                            }


                            MapItem item = new MapItem();
                            if (i == 0)
                            {
                                
                                Label label = new Label();
                                label.Foreground = firs.Count > 1 ? (Brush)mainViewResources["VatsimUirTextBrush"] : (Brush)mainViewResources["PrimaryDarkBrush"];
                                //label.FontFamily = (FontFamily)Application.Current.Resources["Slopes"];
                                //label.FontSize = 16;
                                label.FontWeight = FontWeights.Bold;
                                label.Padding = new Thickness(0, 0, 0, 2);


                                label.Content = controller.callsign.Replace("_", "__");

                                ToolTip tooltip = new ToolTip();
                                tooltip.Content =
                                    $"{controller.callsign}\n{firMetadataTuple.firName}\n{controller.name}\n{controller.frequency}\nConnected for: {TimeUtils.GetConnectionsSinceFromTimeString(controller.logon_time)}";
                                tooltip.FontWeight = FontWeights.Normal;
                                label.ToolTip = tooltip;
                                stackPanel.Children.Add(label);

                                item.Location = new Location(firMetadataTuple.labelCoordinates[0], firMetadataTuple.labelCoordinates[1]);
                                item.Content = stackPanel;
                                item.HorizontalAlignment = HorizontalAlignment.Center;
                                item.VerticalAlignment = VerticalAlignment.Center;
                                vatsimFIRsTextOverlay.Children.Add(item);
                                i++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, ex.Message);
                    }
                }
            }
        }

        private void DrawAirports(LiveViewViewModel liveViewViewModel)
        {
            vatsimAirportsOverlay.Children.Clear();
            vatsimAppCirclesOverlay.Children.Clear();
            foreach (var ca in liveViewViewModel.ControlledAirports)
            {
                bool isIncludeApp = false;
                var controlledAirport = ca.Value;

                HashSet<int> facilities = new HashSet<int>();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{controlledAirport.Airport.ICAO} {controlledAirport.Airport.Name}");
                sb.AppendLine();
                sb.AppendLine("Controllers:");
                foreach (var controller in controlledAirport.Controllers)
                {
                    facilities.Add(controller.facility);

                    DateTime specifiedTime = DateTime.Parse(controller.logon_time, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    DateTime currentTime = DateTime.UtcNow;
                    TimeSpan timeDifference = currentTime - specifiedTime;

                    sb.AppendLine($"{controller.callsign} {controller.name} {controller.frequency} Connected for: {TimeUtils.GetConnectionsSinceFromTimeString(controller.logon_time)}");
                    if (controller.facility == 5)
                    {
                        isIncludeApp = true;
                    }
                }

                foreach (var atis in controlledAirport.Atis)
                {
                    if (atis.text_atis != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"{atis.callsign} {atis.name} {atis.frequency}:");
                        foreach (var message in atis.text_atis)
                        {
                            sb.AppendLine(message);
                        }
                    }


                }

                var imageSource = new BitmapImage(new Uri(Consts.towerRadarImage,
                    UriKind.Absolute));
                if (facilities.Contains(5))
                {
                    if (facilities.Contains(3) || facilities.Contains(4))
                    {
                        imageSource = new BitmapImage(new Uri(Consts.towerRadarImage,
                            UriKind.Absolute));
                    } 
                    else if (facilities.Contains(2) || controlledAirport.Atis.Count > 0)
                    {
                        imageSource = new BitmapImage(new Uri(Consts.radioRadarImage,
                            UriKind.Absolute));
                    }
                }
                else
                {
                    if (facilities.Contains(3) || facilities.Contains(4))
                    {
                        imageSource = new BitmapImage(new Uri(Consts.towerImage,
                            UriKind.Absolute));
                    }
                    else if (facilities.Contains(2) || controlledAirport.Atis.Count > 0)
                    {
                        imageSource = new BitmapImage(new Uri(Consts.radioImage,
                            UriKind.Absolute));
                    }
                }

                var image = new Image()
                {
                    Height = 32,
                    Width = 32,
                    Source = imageSource
                };

                if (isIncludeApp)
                {
                    MapPolygon circlePolygon = new MapPolygon
                    {
                        Stroke = (Brush)mainViewResources["VatsimAppCircleStrokeBrush"],
                        Fill = (Brush)mainViewResources["VatsimAppCircleFillBrush"],
                        StrokeThickness = 2,
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

                    vatsimAppCirclesOverlay.Children.Add(circlePolygon);
                }
                
                MapItem mi = new MapItem
                {
                    Location = new Location(controlledAirport.Airport.Latitude, controlledAirport.Airport.Longitude),
                    Content = image,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
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
                    Margin = new Thickness(-16,-16,0,0)
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
                    sb.AppendLine($"{pilot.flight_plan.aircraft_short}  {pilot.flight_plan.aircraft}");
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

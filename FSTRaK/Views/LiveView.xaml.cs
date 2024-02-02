using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Imaging;
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
                            if (liveViewViewModel.IsShowVatsimFirs)
                            {
                                //DrawFirs(liveViewViewModel);
                            }
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
            vatsimUIRsOverlay.Children.Clear();
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
                                if (firs.Count > 1)
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
    }
}

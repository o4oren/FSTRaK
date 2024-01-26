using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MapControl;
using System;

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
                if (DataContext != null && e.PropertyName == "AirplaneIcon")
                {
                    geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
                    AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;
                }

                else if (DataContext != null && e.PropertyName == "VatsimData")
                {
                    DrawPilots();
                }
            });
        }

        private void DrawPilots()
        {
            vatsimAircraftOverlay.Children.Clear();
            foreach (var pilot in ((LiveViewViewModel)DataContext).VatsimData.pilots)
            {
                Canvas canvas = new Canvas
                {
                    Width = 32,
                    Height = 32
                };
                Path path = new Path
                {
                    Stroke = Brushes.LightBlue,
                    StrokeThickness = 1,
                    Fill = Brushes.Blue,
                    Data = (Geometry)mainViewResources["B737"]
                };

                // Add the Path to the Canvas
                canvas.Children.Add(path);
                MapItem mi = new MapItem
                {
                    Location = new Location(pilot.latitude, pilot.longitude),
                    Content = canvas
                };
                mi.Margin = new Thickness(-16, -16, 0, 0);

                var rotateTransfom = new RotateTransform(pilot.heading, 16, 16);
                // var scaleTransfom = new ScaleTransform(xMap.ZoomLevel / 8 , xMap.ZoomLevel / 8 );
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(rotateTransfom);
                // transformGroup.Children.Add(scaleTransfom);
                mi.RenderTransform = transformGroup;
                
                ToolTip toolTip = new ToolTip();
                toolTip.Content = $"{pilot.name}";
                mi.ToolTip = toolTip;
                vatsimAircraftOverlay.Children.Add(mi);
            }
        }
    }
}

using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
                if (DataContext != null && e.PropertyName == "AirplaneIcon")
                {
                    geometry = Application.Current.Resources[((LiveViewViewModel)DataContext).AirplaneIcon];
                    AirplaneGeometry.Data = (System.Windows.Media.Geometry)geometry;
                }

                else if (DataContext != null && e.PropertyName == "VatsimData")
                {
                    vatsimAircraftOverlay.Children.Clear();
                    foreach (var pilot in ((LiveViewViewModel)DataContext).VatsimData.pilots)
                    {
                        Canvas c = new Canvas();
                        Path path = new Path
                        {
                            Stroke = Brushes.LightBlue,
                            StrokeThickness = 1,
                            Fill = Brushes.Blue,
                        };
                        path.Data = (Geometry)mainViewResources["B737"];

                        // Add the Path to the Canvas
                        c.Children.Add(path);
                        MapItem mi = new MapItem();
                        mi.Location = new Location(pilot.latitude, pilot.longitude);
                        mi.Content = c;

                        var rotateTransfom = new RotateTransform(pilot.heading, 16, 16);
                        mi.RenderTransform = rotateTransfom;


                        // Add the Canvas to the MapControl
                        vatsimAircraftOverlay.Children.Add(mi);
                    }
                }
            });
        }
    }
}

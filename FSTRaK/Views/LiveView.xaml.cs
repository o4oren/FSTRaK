using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Input;

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
            });
        }
    }
}

using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
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
                        default:
                            break;

                    }
                }
            });
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}

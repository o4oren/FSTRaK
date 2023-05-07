using MapControl;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.UI.Xaml;

namespace FSTRaK
{
    /// <summary>
    /// Interaction logic for FlightData.xaml
    /// </summary>
    public partial class FlightData : UserControl
    {
        internal string SessionKey { get; set; }
        public FlightData()
        {
            SessionKey = string.Empty;
            InitializeComponent();
            ((FlightDataViewModel)this.DataContext).PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            xMap.Center = new Location(((FlightDataViewModel)this.DataContext).Position[0], ((FlightDataViewModel)this.DataContext).Position[1]);
        }
    }
}

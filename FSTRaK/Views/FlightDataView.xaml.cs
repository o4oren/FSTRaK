using FSTRaK.ViewModels;
using MapControl;
using System.Windows.Controls;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for FlightData.xaml
    /// </summary>
    public partial class FlightDataView : UserControl
    {
        public FlightDataView()
        {
            InitializeComponent();
            ((FlightDataViewModel)this.DataContext).PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "Aircraft")
            {
                xMap.Center = new Location(((FlightDataViewModel)this.DataContext).Aircraft.Position[0], ((FlightDataViewModel)this.DataContext).Aircraft.Position[1]);
            }
        }
    }
}

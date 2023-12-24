using FSTRaK.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for StatisticsView.xaml
    /// </summary>
    public partial class StatisticsView : UserControl
    {
        public StatisticsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object s, RoutedEventArgs e)
        {
            ((StatisticsViewModel)DataContext).PropertyChanged += DataModel_OnPropertyChange;
        }

        private void OnUnLoaded(object s, RoutedEventArgs e)
        {
            ((StatisticsViewModel)DataContext).PropertyChanged -= DataModel_OnPropertyChange;

        }

        private void DataModel_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "AirlineDistribution":
                    var airlineDistributionDictionary = ((StatisticsViewModel)DataContext).AirlineDistribution;

                    if (airlineDistributionDictionary != null && airlineDistributionDictionary.Any())
                    {
                        var plt = AirlineDistributionChart.Plot;

                        double[] values = airlineDistributionDictionary.Values.ToArray();
                        string[] labels = airlineDistributionDictionary.Keys.ToArray();
                        var pie = plt.AddPie(values);
                        pie.SliceLabels = labels;
                        pie.Explode = false;

                        AirlineDistributionChart.Refresh();
                    }
                    break;

                case "AircraftDistribution":
                    var aircraftDistributionDictionary = ((StatisticsViewModel)DataContext).AircraftDistribution;

                    if (aircraftDistributionDictionary != null && aircraftDistributionDictionary.Any())
                    {
                        var plt = AircraftDistributionChart.Plot;
                        double[] values = aircraftDistributionDictionary.Values.ToArray();
                        string[] labels = aircraftDistributionDictionary.Keys.ToArray();

                        var pie = plt.AddPie(values);
                        pie.SliceLabels = labels;
                        pie.Explode = false;
                        

                        AircraftDistributionChart.Refresh();
                    }
                    break;
            }
        }
    }
}

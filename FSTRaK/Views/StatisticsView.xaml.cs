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
using ScottPlot;
using ScottPlot.Plottable;
using FSTRaK.Utils;
using System.Drawing;
using static System.Resources.ResXFileRef;
using Color = System.Drawing.Color;

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
            ((StatisticsViewModel)DataContext).PropertyChanged += (s, e) =>
            {
                if (DataContext == null)
                    return;
                switch (e.PropertyName)
                {
                    case "AirlineDistribution":
                        var airlineDistributionDictionary = ((StatisticsViewModel)DataContext).AirlineDistribution;

                        if (airlineDistributionDictionary != null && airlineDistributionDictionary.Any())
                        {
                            GeneratePie(airlineDistributionDictionary, AirlineDistributionChart);
                        }
                        break;

                    case "AircraftDistribution":
                        var aircraftDistributionDictionary = ((StatisticsViewModel)DataContext).AircraftDistribution;

                        if (aircraftDistributionDictionary != null && aircraftDistributionDictionary.Any())
                        {
                            GeneratePie(aircraftDistributionDictionary, AircraftDistributionChart);
                        }
                        break;
                }
            };
            ((StatisticsViewModel)DataContext).ViewLoaded();
        }




        private void GeneratePie(Dictionary<string, double> data, WpfPlot chart)
        {
            var plt = chart.Plot;
            plt.Clear();
            chart.Plot.Style(
                figureBackground: System.Drawing.Color.Transparent,
                dataBackground: System.Drawing.Color.Transparent
            );
            double[] values = data.Values.ToArray();
            string[] labels = data.Keys.ToArray();
            

            var pie = plt.AddPie(values);
            pie.SliceLabels = labels;
            pie.Explode = false;

            // pie.ShowPercentages = true;
            pie.ShowValues = true;
            // pie.ShowLabels = true;
            //pie.SliceLabelPosition = 0.5;
            //pie.Size = .7;
            
            pie.SliceLabelColors = Enumerable.Repeat(ColorTranslator.FromHtml("Black"), pie.SliceFillColors.Length).ToArray();

            var legend = chart.Plot.Legend();
            chart.Configuration.Pan = false;
            chart.Configuration.Zoom = false;

            chart.Refresh();
        }
    }
}

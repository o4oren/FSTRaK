using FSTRaK.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using FSTRaK.Utils;
using System.Drawing;
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
                figureBackground: Color.Transparent,
                dataBackground: Color.Transparent
            );
            double[] values = data.Values.ToArray();
            string[] labels = data.Keys.ToArray();

            Color color1 = ColorUtil.GetDrawingColorFromResource("ChartColor1");
            Color color2 = ColorUtil.GetDrawingColorFromResource("ChartColor2");
            Color color3 = ColorUtil.GetDrawingColorFromResource("ChartColor3");
            Color color4 = ColorUtil.GetDrawingColorFromResource("ChartColor4");
            Color color5 = ColorUtil.GetDrawingColorFromResource("ChartColor5");
            Color color6 = ColorUtil.GetDrawingColorFromResource("ChartColor6");

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
            pie.SliceFillColors = new Color[] { color1, color2, color3, color4, color5, color6 };

            chart.Refresh();
        }
    }
}

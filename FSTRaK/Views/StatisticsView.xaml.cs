using System;
using FSTRaK.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using FSTRaK.Utils;
using System.Drawing;
using Color = System.Drawing.Color;
using System.Windows.Input;
using FSTRaK.DataTypes;

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

                    case "FrequentDepartureAirportsDistribution":
                        var depDistributionDeictionary = ((StatisticsViewModel)DataContext).FrequentDepartureAirportsDistribution;

                        if (depDistributionDeictionary != null && depDistributionDeictionary.Any())
                        {
                            GeneratePie(depDistributionDeictionary, DepDistributionChart);
                        }
                        break;

                    case "FrequentArrivalAirportsDistribution":
                        var arrDistributionDictionary = ((StatisticsViewModel)DataContext).FrequentArrivalAirportsDistribution;

                        if (arrDistributionDictionary != null && arrDistributionDictionary.Any())
                        {
                            GeneratePie(arrDistributionDictionary, ArrDistributionChart);
                        }
                        break;

                    case "FlightsPerDay":
                    case "TimePeriod":
                        var arrFlightsPerDay = ((StatisticsViewModel)DataContext).FlightsPerDay;

                        if (arrFlightsPerDay != null && arrFlightsPerDay.Any())
                        {
                            GenerateHistogram(arrFlightsPerDay, ArrFlightsPerDay, ((StatisticsViewModel)DataContext).TimePeriod);
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

        private void GenerateHistogram(Dictionary<DateTime, double> data, WpfPlot chart, TimePeriod timePeriod)
        {
            var plt = chart.Plot;
            plt.Clear();
            chart.Plot.Style(
                figureBackground: Color.Transparent,
                dataBackground: Color.Transparent
            );

            if (timePeriod == TimePeriod.Month)
            {
                data = AggregateDataByMonth(data);
            }


            if (timePeriod == TimePeriod.Year)
            {
                data = AggregateDataByYear(data);
            }

            double[] values = data.Values.ToArray();
            double[] timePoints = data.Keys.Select(d => d.ToOADate()).ToArray();

            Color color1 = ColorUtil.GetDrawingColorFromResource("ChartColor1");
            Color color2 = ColorUtil.GetDrawingColorFromResource("ChartColor2");
            Color color3 = ColorUtil.GetDrawingColorFromResource("ChartColor3");
            Color color4 = ColorUtil.GetDrawingColorFromResource("ChartColor4");
            Color color5 = ColorUtil.GetDrawingColorFromResource("ChartColor5");
            Color color6 = ColorUtil.GetDrawingColorFromResource("ChartColor6");

            var bar = plt.AddBar(values, timePoints);
            
            var xAxisMin = timePoints.Min() - 1;
            var xAxisMax = timePoints.Max() + 1;

            if (timePeriod == TimePeriod.Day)
            {
                plt.XAxis.TickLabelFormat("dd/MMM/yyyy", true);
                plt.XAxis.SetZoomInLimit(14);
                plt.SetAxisLimits(xAxisMax - 60, xAxisMax);

            }
            else if (timePeriod == TimePeriod.Month)
            {
                plt.XAxis.TickLabelFormat("MMM yyyy", true);

                xAxisMin = timePoints.Min() - 30;
                    xAxisMax = timePoints.Max() + 30;

                    plt.SetAxisLimits(xAxisMax - 365, xAxisMax);
                    plt.XAxis.SetZoomInLimit(12 * 31);

                for (int i = 0; i < timePoints.Length; i++)
                {
                    DateTime month = data.Keys.ElementAt(i);
                    int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
                    double monthWidth = daysInMonth; // Each day is one unit in OADate
                    bar.BarWidth = monthWidth - 10; // Set the bar width
                    

                }

            }
            else if (timePeriod == TimePeriod.Year)
            {
                plt.XAxis.TickLabelFormat("yyyy", true);
                double yearWidth = 365; // Each day is one unit in OADate
                bar.BarWidth = yearWidth - 100; // Set the bar width

                xAxisMin = timePoints.Min() - 300;
                xAxisMax = timePoints.Max() + 300;

                plt.SetAxisLimits(xAxisMax - 365, xAxisMax);
                plt.XAxis.SetZoomInLimit(12 * 365);
                plt.SetAxisLimits(xAxisMax - (365 * 12), xAxisMax);
            }


            chart.Configuration.Pan = true;
            chart.Configuration.LockVerticalAxis = true;
            chart.Configuration.Zoom = true;
            //plt.XAxis.SetZoomInLimit(14);
            // plt.XAxis.SetZoomOutLimit(CalculateNumberOfDays(data));

            chart.MouseWheel += (sender, e) =>
            {
                if (Keyboard.Modifiers != ModifierKeys.Control)
                {
                    e.Handled = true; // Prevent panning
                }
            };

            //chart.AxesChanged += (s, e) =>
            //{

            //    double xMin = plt.GetAxisLimits().XMin;
            //    double xMax = plt.GetAxisLimits().XMax;
                
            //    var zoomLevel = xMax - xMin;

            //    if (xMin < xAxisMin)
            //    {
            //        plt.SetAxisLimits(xAxisMin, xAxisMin + (xMax - xMin) + 1);
            //    }
            //    else if (xMax > xAxisMax)
            //    {
            //        plt.SetAxisLimits(xAxisMax - (xMax - xMin) - 1, xAxisMax);
            //    }
            //};

            chart.Refresh();
        }

        private double? CalculateNumberOfDays(Dictionary<DateTime, double> data)
        {
            var minDay = data.Keys.Min().Date;
            var maxDay = data.Keys.Max().Date;
            TimeSpan duration = maxDay - minDay;
            int numberOfDays = duration.Days;
            return numberOfDays;
        }

        private Dictionary<DateTime, double> AggregateDataByYear(Dictionary<DateTime, double> data)
        {
            return data
                .GroupBy(x => new DateTime(x.Key.Year, 1, 1))
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Value)
                );
        }

        private Dictionary<DateTime, double> AggregateDataByMonth(Dictionary<DateTime, double> data)
        {
            return data
                .GroupBy(x => new DateTime(x.Key.Year, x.Key.Month, 1))
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Value)
                );
        }


    }
}

using FSTRaK.ViewModels;
using MapControl;
using ScottPlot;
using ScottPlot.Plottable;
using ScottPlot.Renderable;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FSTRaK.Utils;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for FlightDetailsView.xaml
    /// </summary>
    public partial class FlightDetailsView : UserControl
    {
        public FlightDetailsView()
        {
            InitializeComponent();
            LogbookMap.AnimationDuration = new TimeSpan(0, 0, 0, 0, 600);
        }

        private void OnLoaded(object s, RoutedEventArgs e)
        {
            ((FlightDetailsViewModel)DataContext).PropertyChanged += DataModel_OnPropertyChange;

            var graphColor = ResourceUtils.GetColorFromResource("BorderLightColor");

            AltSpeedChart.Plot.XAxis.DateTimeFormat(true);
            AltSpeedChart.Plot.YAxis.Label("Altitude");
            AltSpeedChart.Plot.YAxis2.Label("Ground Speed");
            AltSpeedChart.Plot.YAxis2.Ticks(true);

            
            var legend = AltSpeedChart.Plot.Legend();

            legend.FontBold = true;

            legend.FontColor = graphColor;
                
            legend.FillColor = Color.Transparent;
            legend.OutlineColor = graphColor;
            AltSpeedChart.Plot.Style(ScottPlot.Style.Black);
            AltSpeedChart.Plot.Style(
                figureBackground: Color.Transparent,
                dataBackground: Color.Transparent
                );
            AltSpeedChart.Plot.XAxis.Color(graphColor);
            AltSpeedChart.Plot.XAxis2.Color(graphColor);

            AltSpeedChart.Plot.YAxis.Color(graphColor);
            AltSpeedChart.Plot.YAxis2.Color(graphColor);

            

        }

        private void OnUnLoaded(object s, RoutedEventArgs e)
        {
            ((FlightDetailsViewModel)DataContext).PropertyChanged -= DataModel_OnPropertyChange;

        }


        private void DataModel_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ViewPort":
                    var viewPort = ((FlightDetailsViewModel)DataContext).ViewPort;
                    if (viewPort != null)
                    {
                        ZoomToBounds(viewPort);
                    }
                    break;
                case "AltSpeedGroundAltDictionary":
                    var altSpeedGroundSeries = ((FlightDetailsViewModel)DataContext).AltSpeedGroundAltDictionary;

                    if (altSpeedGroundSeries != null)
                    {
                        var timeX = altSpeedGroundSeries.Keys.ToArray();
                        var altY = altSpeedGroundSeries.Values.Select(v => v[0]).ToArray();
                        var speedY = altSpeedGroundSeries.Values.Select(v => v[1]).ToArray();
                        var groundAltY = altSpeedGroundSeries.Values.Select(v => v[2]).ToArray();
                        AltSpeedChart.Plot.Clear();

                        var altPlot = AltSpeedChart.Plot.AddScatter(timeX, altY);
                        altPlot.Label ="Altitude";
                        altPlot.Smooth = false;
                        altPlot.MarkerSize = 0;
                        altPlot.LineWidth = 2;

                        var gAltPlot = AltSpeedChart.Plot.AddScatter(timeX, groundAltY);
                        gAltPlot.Label =  "Ground Altitude";
                        gAltPlot.Smooth = true;
                        gAltPlot.MarkerSize = 0;
                        gAltPlot.LineWidth = 2;

                        var speedPlot = AltSpeedChart.Plot.AddScatter(timeX, speedY);
                        speedPlot.Label = "Ground Speed";
                        speedPlot.Smooth = false;
                        speedPlot.YAxisIndex = 1;
                        speedPlot.Smooth = false;
                        speedPlot.MarkerSize = 0;
                        speedPlot.LineWidth = 2;



                        var legend = AltSpeedChart.Plot.Legend();
                        legend.Location = Alignment.UpperLeft;

                        AltSpeedChart.Refresh();
                    }
                    break;


            }
        }


        public void ZoomToBounds(BoundingBox boundingBox)
        {
            var rect = LogbookMap.MapProjection.BoundingBoxToMapRect(boundingBox);
            var scale = Math.Min(LogbookMap.ActualWidth / rect.Width, LogbookMap.ActualHeight / rect.Height);
            var zoomLevel = ViewTransform.ScaleToZoomLevel(scale);
            // Set new view
            LogbookMap.TargetZoomLevel = Math.Floor(Math.Min(16, zoomLevel) - 1);
            LogbookMap.TargetCenter = LogbookMap.MapProjection.MapToLocation(rect.Center);
            LogbookMap.TargetHeading = 0d;
        }

    }
}

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
using System.Windows.Navigation;
using FSTRaK.Utils;
using System.Diagnostics;
using ScottPlot.Drawing.Colormaps;

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

            var graphColor = ColorUtil.GetDrawingColorFromResource("BorderLightColor");

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
            var rect = LogbookMap.MapProjection.BoundingBoxToMap(boundingBox);
            if (rect != null && !Double.IsInfinity(boundingBox.Width))
            {
                // scale padding

                double halfLat = (boundingBox.North - boundingBox.South);
                double halfLon = (boundingBox.East - boundingBox.West);

                var paddedNorth = boundingBox.Center.Latitude + halfLat;
                var paddedSouth = boundingBox.Center.Latitude - halfLat;
                var paddedEast = boundingBox.Center.Longitude + halfLon;
                var paddedWest = boundingBox.Center.Longitude - halfLon;

                paddedNorth = Math.Min(90, paddedNorth);
                paddedSouth = Math.Max(-90, paddedSouth);
                paddedEast = Math.Min(180, paddedEast);
                paddedWest = Math.Max(-180, paddedWest);

                var paddedBoundingBox = new BoundingBox(paddedSouth, paddedWest, paddedNorth, paddedEast);

                LogbookMap.TargetHeading = 0d;
                LogbookMap.TargetZoomLevel = 5;

                LogbookMap.ZoomToBounds(paddedBoundingBox);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}

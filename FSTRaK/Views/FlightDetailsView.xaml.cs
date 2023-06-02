using FSTRaK.ViewModels;
using MapControl;
using ScottPlot.Plottable;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
            logbookMap.AnimationDuration = new TimeSpan(0, 0, 0, 0, 600);

                
        }



        private void OnLoaded(object s, RoutedEventArgs e)
        {
            ((FlightDetailsViewModel)DataContext).PropertyChanged += DataModel_OnPropertyChange;
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
                        double[] timeX = altSpeedGroundSeries.Keys.ToArray();
                        double[] altY = altSpeedGroundSeries.Values.Select(v => v[0]).ToArray();
                        double[] speedY = altSpeedGroundSeries.Values.Select(v => v[1]).ToArray();
                        double[] groundAltY = altSpeedGroundSeries.Values.Select(v => v[2]).ToArray();
                        AltSpeedChart.Plot.Clear();

                        var plot = AltSpeedChart.Plot.AddScatter(timeX, altY);
                        AltSpeedChart.Plot.XAxis.DateTimeFormat(true);
                        plot.Smooth = true;
                        plot.MarkerSize = 0;
                        // var plot1 = AltSpeedChart.Plot.AddScatter(speedY, altY);
                        // var plot2 = AltSpeedChart.Plot.AddScatter(groundAltY, altY);

                        AltSpeedChart.Refresh();

                    }

                    break;
            }
        }


        public void ZoomToBounds(BoundingBox boundingBox)
        {
            var rect = logbookMap.MapProjection.BoundingBoxToMapRect(boundingBox);
            var scale = Math.Min(logbookMap.ActualWidth / rect.Width, logbookMap.ActualHeight / rect.Height);
            var zoomLevel = ViewTransform.ScaleToZoomLevel(scale);
            // Set new view
            logbookMap.TargetZoomLevel = Math.Floor(Math.Min(16, zoomLevel) - 1);
            logbookMap.TargetCenter = logbookMap.MapProjection.MapToLocation(rect.Center);
            logbookMap.TargetHeading = 0d;
        }

    }
}

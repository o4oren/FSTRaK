using FSTRaK.ViewModels;
using MapControl;
using System;
using System.ComponentModel;
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
            if (e.PropertyName == "ViewPort")
            {
                var viewPort = ((FlightDetailsViewModel)DataContext).ViewPort;
                if (viewPort != null)
                {
                    ZoomToBounds(viewPort);
                }
            }
        }


        public void ZoomToBounds(BoundingBox boundingBox)
        {
            var rect = logbookMap.MapProjection.BoundingBoxToMapRect(boundingBox);
            var scale = Math.Min(logbookMap.ActualWidth / rect.Width, logbookMap.ActualHeight / rect.Height);
            var zoomLevel = ViewTransform.ScaleToZoomLevel(10);
            // Set new view
            logbookMap.TargetZoomLevel = Math.Floor(Math.Min(16, zoomLevel) - 1);
            logbookMap.TargetCenter = logbookMap.MapProjection.MapToLocation(rect.Center);
            logbookMap.TargetHeading = 0d;
        }

    }
}

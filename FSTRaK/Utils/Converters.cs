using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FSTRaK.Utils;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Hidden : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ResourceNameToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string pathDataKey)
        {
            if (Application.Current.TryFindResource(pathDataKey) is Geometry pathGeometry)
            {
                return pathGeometry;
            }
        }
        return DependencyProperty.UnsetValue; // Indicates a failed conversion
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
    
}

public class ResourceNameToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string pathDataKey)
        {
            if (Application.Current.TryFindResource(pathDataKey) is BitmapImage pathImage)
            {
                return pathImage;
            }
        }
        return DependencyProperty.UnsetValue; // Indicates a failed conversion
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

}
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FSTRaK.Utils;

public class ResourceNameToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string pathDataKey)
        {
            if (Application.Current.TryFindResource(pathDataKey) is Geometry pathGeometry)
            {
                return pathGeometry.Clone();
            }
        }
        return DependencyProperty.UnsetValue; // Indicates a failed conversion
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
    
}
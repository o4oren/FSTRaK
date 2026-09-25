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
        => (value == null || (value is string s && string.IsNullOrEmpty(s)))
           ? Visibility.Collapsed
           : Visibility.Visible;

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

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Subtracts every later value from the first, minus a constant passed as the parameter.
/// Used to give the expanded statistics map whatever height the panels above it leave,
/// rather than the whole viewport - which is what pushed it past the bottom edge.
/// </summary>
public class RemainingHeightConverter : IMultiValueConverter
{
    private const double MinimumHeight = 200;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0 || !(values[0] is double available))
        {
            return MinimumHeight;
        }

        var remaining = available;
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] is double used)
            {
                remaining -= used;
            }
        }

        if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var padding))
        {
            remaining -= padding;
        }

        return remaining < MinimumHeight ? MinimumHeight : remaining;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InvertedNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value == null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr)
        {
            try
            {
                return new SolidColorBrush(
                    (System.Windows.Media.Color)ColorConverter.ConvertFromString(colorStr));
            }
            catch { }
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
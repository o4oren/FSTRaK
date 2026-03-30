using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FSTRaK.DataTypes;

namespace FSTRaK.Utils
{
    public class NetworkTypeToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is NetworkType n && n != NetworkType.None ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

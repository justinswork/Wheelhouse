using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wheelhouse.UI.Converters;

public sealed class BoolToRowHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            if (parameter is string s && double.TryParse(s, out var height))
                return new GridLength(height);
            return new GridLength(200);
        }
        return new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

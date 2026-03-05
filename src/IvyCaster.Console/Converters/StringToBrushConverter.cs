using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IvyCaster.Console;

public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IvyCaster.Console;

public sealed class TreeDepthToMarginConverter : IValueConverter
{
    private const double IndentSize = 20.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DependencyObject item)
        {
            var depth = 0;
            var parent = ItemsControl.ItemsControlFromItemContainer(item);
            while (parent is TreeViewItem)
            {
                depth++;
                parent = ItemsControl.ItemsControlFromItemContainer(parent);
            }
            return new Thickness(depth * IndentSize, 0, 0, 0);
        }

        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

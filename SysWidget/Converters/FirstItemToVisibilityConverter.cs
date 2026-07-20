using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SysWidget.Converters;

/// <summary>
/// Maps an ItemsControl alternation index to <see cref="Visibility"/>: index 0 (first item)
/// collapses the leading separator; every other item shows it.
/// </summary>
public sealed class FirstItemToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int index = value is int i ? i : 0;
        return index == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

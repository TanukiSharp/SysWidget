using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace SysWidget.Converters;

/// <summary>
/// Maps a fraction in [0,1] to a fill brush that runs green → yellow → red as it rises,
/// so a gauge reads "healthy" when low and "hot" when high.
/// </summary>
public sealed class FractionToColorConverter : IValueConverter
{
    private static readonly Color Low = Color.FromRgb(0x3F, 0xB9, 0x50);   // green
    private static readonly Color Mid = Color.FromRgb(0xD2, 0xA8, 0x06);   // amber
    private static readonly Color High = Color.FromRgb(0xF8, 0x51, 0x49);  // red

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double t = value is double d && !double.IsNaN(d) ? Math.Clamp(d, 0.0, 1.0) : 0.0;

        Color color = t < 0.5
            ? Lerp(Low, Mid, t / 0.5)
            : Lerp(Mid, High, (t - 0.5) / 0.5);

        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)(a.R + ((b.R - a.R) * t)),
            (byte)(a.G + ((b.G - a.G) * t)),
            (byte)(a.B + ((b.B - a.B) * t)));
    }
}

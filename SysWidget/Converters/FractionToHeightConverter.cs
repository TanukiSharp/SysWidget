using System.Globalization;
using System.Windows.Data;

namespace SysWidget.Converters;

/// <summary>
/// Maps a fraction in [0,1] to a pixel height = fraction × track-height, where the track
/// height is passed as the ConverterParameter. Drives the filled portion of a vertical gauge.
/// </summary>
public sealed class FractionToHeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double t = value is double d && !double.IsNaN(d) ? Math.Clamp(d, 0.0, 1.0) : 0.0;
        double track = parameter is not null
            && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double p)
            ? p
            : 0.0;
        return t * track;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

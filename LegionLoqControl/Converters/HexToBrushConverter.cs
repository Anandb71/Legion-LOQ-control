using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LegionLoqControl.Domain.Controls;

namespace LegionLoqControl.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Fallback = Create(0x18, 0x1E, 0x23);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && RgbColor.TryParseHex(hex, out RgbColor color))
        {
            return Create(color.Red, color.Green, color.Blue);
        }

        return Fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Create(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}

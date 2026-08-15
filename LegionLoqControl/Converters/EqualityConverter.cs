using System.Globalization;
using System.Windows.Data;

namespace LegionLoqControl.Converters;

public sealed class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 2 })
            return false;

        string? current = values[^1]?.ToString();
        for (int index = 0; index < values.Length - 1; index++)
        {
            if (string.Equals(values[index]?.ToString(), current, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

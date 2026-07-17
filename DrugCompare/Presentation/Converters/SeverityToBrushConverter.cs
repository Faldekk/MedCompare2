using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DrugCompare.Features.InteractionChecker;

namespace DrugCompare.Presentation.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var severity = value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

        return severity switch
        {
            "contraindicated" => Brushes.DarkRed,
            "major" => Brushes.Red,
            "moderate" => Brushes.DarkOrange,
            "minor" => Brushes.Goldenrod,
            "unknown" => Brushes.Gray,
            "x" => Brushes.DarkRed,
            "d" => Brushes.Red,
            "c" => Brushes.DarkOrange,
            "b" => Brushes.Goldenrod,
            "a" => Brushes.Gray,
            _ => Brushes.Black
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

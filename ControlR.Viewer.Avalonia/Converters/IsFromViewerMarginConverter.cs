using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;

namespace ControlR.Viewer.Avalonia.Converters;

public class IsFromViewerMarginConverter : IValueConverter
{
  public static readonly IsFromViewerMarginConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is true)
    {
      return new Thickness(80, 0, 0, 10);
    }
    return new Thickness(0, 0, 80, 10);
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    => throw new NotSupportedException();
}

using Avalonia.Data.Converters;
using System.Globalization;

namespace ControlR.DesktopClient.Converters;

public class InverseBooleanConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is bool boolValue)
    {
      return !boolValue;
    }
    return value;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is bool boolValue)
    {
      return !boolValue;
    }
    return value;
  }
}
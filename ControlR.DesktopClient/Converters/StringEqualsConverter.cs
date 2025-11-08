using Avalonia.Data.Converters;
using System.Globalization;

namespace ControlR.DesktopClient.Converters;

public class StringEqualsConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is string stringValue && parameter is string parameterValue)
    {
      return string.Equals(stringValue, parameterValue, StringComparison.Ordinal);
    }
    return false;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}

using Avalonia.Data.Converters;
using System.Globalization;

namespace ControlR.DesktopClient.Converters;

public class EnumComparisonConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is null || parameter is null)
    {
      return false;
    }

    return value.Equals(parameter);
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is true && parameter is not null)
    {
      return parameter;
    }

    return Avalonia.Data.BindingOperations.DoNothing;
  }
}

using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using Avalonia;

namespace ControlR.DesktopClient.Converters;

public class ResourceKeyToGeometryConverter : IValueConverter
{
  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is not string key)
      return null;

    if (Application.Current is null)
    {
      return null;
    }

    if (!Application.Current.Resources.TryGetResource(key, null, out var found))
    {
      return null;
    }

    if (found is StreamGeometry sg)
      return sg;

    if (found is Geometry g)
      return g;

    return null;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

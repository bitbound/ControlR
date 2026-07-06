using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using System.Collections.Concurrent;
using Avalonia.Controls;

namespace ControlR.Viewer.Avalonia.Converters;

/// <summary>
/// Converts a <see cref="SnackbarSeverity"/> to a brush that can be used
/// as the <c>Foreground</c> for a <see cref="TextBlock" /> or other control.
/// </summary>
public class SnackbarSeverityToBrushConverter : IValueConverter
{
  private static readonly ConcurrentDictionary<string, IBrush> _brushesCache = [];

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    if (value is not SnackbarSeverity severity)
      return AvaloniaProperty.UnsetValue;

    var colorKey = severity switch
    {
      SnackbarSeverity.Warning => "WarningColor",
      SnackbarSeverity.Error => "ErrorColor",
      SnackbarSeverity.Success => "SuccessColor",
      _ => "InfoColor",
    };

    var cacheKey = $"{Application.Current?.ActualThemeVariant.Key}-{colorKey}";

    if (_brushesCache.TryGetValue(cacheKey, out var cachedBrush))
      return cachedBrush;

    // Try control-level resource tree first (walks up to application resources)
    if (parameter is StyledElement element
        && element.TryFindResource(colorKey, out var controlResource)
        && controlResource is IBrush controlBrush)
    {
      _brushesCache[cacheKey] = controlBrush;
      return controlBrush;
    }

    // Fallback to application-level resources
    var app = Application.Current;

    if (app?.ActualThemeVariant is { } themeVariant
        && app.Resources.TryGetResource(colorKey, themeVariant, out var color)
        && color is IBrush brush)
    {
      _brushesCache[cacheKey] = brush;
      return brush;
    }

    return AvaloniaProperty.UnsetValue;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
      => throw new NotSupportedException();
}

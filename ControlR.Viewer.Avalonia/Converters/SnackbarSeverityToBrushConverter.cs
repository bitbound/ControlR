using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.VisualTree;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using System.Collections.Concurrent;

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
    {
        return cachedBrush;
    }

    if (parameter is CompiledBindingExtension bindingExt &&
        bindingExt.DefaultAnchor?.Target is Control control)
    {
      var controlrViewer = control.FindAncestorOfType<ControlrViewer>();
      if (controlrViewer?.TryFindResource(colorKey, controlrViewer.ActualThemeVariant, out var color) == true
          && color is IBrush brush)
      {
        _brushesCache[cacheKey] = brush;
        return brush;
      }
    }

    return AvaloniaProperty.UnsetValue;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
      => throw new NotSupportedException();
}

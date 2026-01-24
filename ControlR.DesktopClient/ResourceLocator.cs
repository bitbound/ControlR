using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace ControlR.DesktopClient;

public static class ResourceLocator
{
  private static Dictionary<ThemeVariant, IThemeVariantProvider>? _themedDictionaries;

  static ResourceLocator()
  {
    if (Application.Current is not { } app)
    {
      return;
    }

    app.ResourcesChanged += (_, _) => _themedDictionaries = null;
  }

  public static bool TryGetResource<T>(
    string key,
    [NotNullWhen(true)] out T? resource)
  {
    resource = Dispatcher.UIThread.Invoke(() =>
    {
      if (Application.Current is not { } app)
      {
        return default;
      }

      if (app.Resources.TryGetResource(key, null, out var res)
          && res is T typedResource)
      {
        return typedResource;
      }
      return default;
    });

    return resource is not null;
  }

  public static bool TryGetThemeColorBrush(
    ThemeColor themeColor,
    [NotNullWhen(true)] out SolidColorBrush? themeColorBrush)
  {
    var themeKey = ThemeColorKeys.GetResourceKey(themeColor);
    return TryGetThemedResource(themeKey, out themeColorBrush);
  }

  public static bool TryGetThemedResource<T>(
    string key,
    [NotNullWhen(true)] out T? resource)
  {
    resource = Dispatcher.UIThread.Invoke(() =>
    {
      if (Application.Current is not { } app)
      {
        return default;
      }

      _themedDictionaries ??= app.Resources
        .MergedDictionaries
        .OfType<ResourceDictionary>()
        .SelectMany(md => md.ThemeDictionaries)
        .ToDictionary();

      if (_themedDictionaries.TryGetValue(app.ActualThemeVariant, out var themeDict)
          && themeDict.TryGetResource(key, app.ActualThemeVariant, out var res)
          && res is T typedResource)
      {
        return typedResource;
      }

      return default;
    });

    return resource is not null;
  }
}
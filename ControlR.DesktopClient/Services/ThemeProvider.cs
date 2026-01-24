using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;

namespace ControlR.DesktopClient.Services;

public interface IThemeProvider
{
  event EventHandler? ThemeChanged;
  ThemeVariant CurrentTheme { get; }
  ThemeMode CurrentThemeMode { get; }
  void SetThemeMode(ThemeMode mode);
  void ToggleTheme();
}

public class ThemeProvider : IThemeProvider
{
  private ThemeMode _currentThemeMode = ThemeMode.Dark;

  public event EventHandler? ThemeChanged;

  public ThemeVariant CurrentTheme =>
    _currentThemeMode switch
    {
      ThemeMode.Light => ThemeVariant.Light,
      ThemeMode.Dark => ThemeVariant.Dark,
      _ => ThemeVariant.Default
    };

  public ThemeMode CurrentThemeMode
  {
    get => _currentThemeMode;
    private set
    {
      if (_currentThemeMode != value)
      {
        _currentThemeMode = value;
        ApplyTheme();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
      }
    }
  }
  public void SetThemeMode(ThemeMode mode)
  {
    CurrentThemeMode = mode;
  }

  public void ToggleTheme()
  {
    CurrentThemeMode = _currentThemeMode switch
    {
      ThemeMode.Light => ThemeMode.Dark,
      ThemeMode.Dark => ThemeMode.Light,
      _ => ThemeMode.Dark
    };
  }

  // I ran into issues with system theme detection on Windows and Wayland, so this is disabled for now.
  // On Windows, it's due to how we're launching via cloning winlogon token.
  //private static ThemeVariant GetSystemTheme()
  //{
  //  // On Windows and Linux, theme detection isn't working yet. Default to Dark.
  //  if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
  //  {
  //    return ThemeVariant.Dark;
  //  }

  //  try
  //  {
  //    var platformSettings = Application.Current?.PlatformSettings;
  //    if (platformSettings is not null)
  //    {
  //      var colorScheme = platformSettings.GetColorValues();
  //      // Convert PlatformThemeVariant enum to ThemeVariant class
  //      return colorScheme.ThemeVariant == PlatformThemeVariant.Dark
  //        ? ThemeVariant.Dark
  //        : ThemeVariant.Light;
  //    }
  //  }
  //  catch
  //  {
  //    // If detection fails, default to dark
  //  }

  //  return ThemeVariant.Dark;
  //}

  private void ApplyTheme()
  {
    Application.Current?.RequestedThemeVariant = CurrentTheme;
  }
}
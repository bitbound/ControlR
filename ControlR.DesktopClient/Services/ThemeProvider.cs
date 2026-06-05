using Avalonia;
using Avalonia.Styling;

namespace ControlR.DesktopClient.Services;

public interface IThemeProvider
{
  event EventHandler? ThemeChanged;
  ThemeVariant CurrentTheme { get; }
  AvaloniaThemeMode CurrentThemeMode { get; }
  void SetThemeMode(AvaloniaThemeMode mode);
  void ToggleTheme();
}

public class ThemeProvider : IThemeProvider
{
  private AvaloniaThemeMode _currentThemeMode = AvaloniaThemeMode.Dark;

  public event EventHandler? ThemeChanged;

  public ThemeVariant CurrentTheme =>
    _currentThemeMode switch
    {
      AvaloniaThemeMode.Light => ThemeVariant.Light,
      AvaloniaThemeMode.Dark => ThemeVariant.Dark,
      _ => ThemeVariant.Default
    };

  public AvaloniaThemeMode CurrentThemeMode
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
  public void SetThemeMode(AvaloniaThemeMode mode)
  {
    CurrentThemeMode = mode;
  }

  public void ToggleTheme()
  {
    CurrentThemeMode = _currentThemeMode switch
    {
      AvaloniaThemeMode.Light => AvaloniaThemeMode.Dark,
      AvaloniaThemeMode.Dark => AvaloniaThemeMode.Light,
      _ => AvaloniaThemeMode.Dark
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
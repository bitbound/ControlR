using ControlR.Web.Client.Components.Layout;

namespace ControlR.Web.Client.Services;

/// <summary>
/// A provider for the current theme state (dark/light/auto);
/// Values are updated by <see cref="BaseLayout"/>.
/// </summary>
public interface IThemeStateProvider
{
  ThemeMode CurrentThemeMode { get; }
  bool IsDarkMode { get; }

  void SetIsDarkMode(bool isDarkMode);
  void SetThemeMode(ThemeMode mode);
}

public sealed class ThemeStateProvider : IThemeStateProvider
{
  private readonly Lock _lock = new();

  private ThemeMode _currentThemeMode = ThemeMode.Auto;
  private bool _isDarkMode = true;

  public ThemeMode CurrentThemeMode
  {
    get
    {
      lock (_lock)
      {
        return _currentThemeMode;
      }
    }
  }
  public bool IsDarkMode
  {
    get
    {
      lock (_lock)
      {
        return _isDarkMode;
      }
    }
  }

  public void SetIsDarkMode(bool isDarkMode)
  {
    lock (_lock)
    {
      _isDarkMode = isDarkMode;
    }
  }

  public void SetThemeMode(ThemeMode mode)
  {
    lock (_lock)
    {
      _currentThemeMode = mode;
    }
  }
}

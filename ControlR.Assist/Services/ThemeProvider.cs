using Avalonia;
using Avalonia.Styling;
using ControlR.Libraries.Avalonia.Theming;

namespace ControlR.Assist.Services;

public interface IThemeProvider
{
  event EventHandler? ThemeChanged;
  ThemeVariant CurrentTheme { get; }
  AvaloniaThemeMode CurrentThemeMode { get; }
  void SetThemeMode(AvaloniaThemeMode mode);
}

public sealed class ThemeProvider(IAssistSettingsService settingsService) : IThemeProvider
{
  private readonly IAssistSettingsService _settingsService = settingsService;
  private AvaloniaThemeMode _currentThemeMode = settingsService.GetThemeMode();

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
      if (_currentThemeMode == value)
      {
        return;
      }

      _currentThemeMode = value;
      _settingsService.SetThemeMode(value);
      Application.Current?.RequestedThemeVariant = CurrentTheme;
      ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
  }

  public void SetThemeMode(AvaloniaThemeMode mode) => CurrentThemeMode = mode;
}

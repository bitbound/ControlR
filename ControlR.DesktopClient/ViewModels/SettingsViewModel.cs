using ControlR.DesktopClient.Common.ViewModels;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface ISettingsViewModel : IViewModelBase
{
  bool IsDarkModeChecked { get; set; }
  bool IsLightModeChecked { get; set; }
}

public partial class SettingsViewModel(IThemeProvider themeProvider) : ViewModelBase<SettingsView>, ISettingsViewModel
{
  private readonly IThemeProvider _themeProvider = themeProvider;

  public bool IsDarkModeChecked
  {
    get => _themeProvider.CurrentThemeMode == Libraries.Avalonia.Theming.AvaloniaThemeMode.Dark;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(Libraries.Avalonia.Theming.AvaloniaThemeMode.Dark);
      }
      OnPropertyChanged(nameof(IsLightModeChecked));
      OnPropertyChanged(nameof(IsDarkModeChecked));
    }
  }

  public bool IsLightModeChecked
  {
    get => _themeProvider.CurrentThemeMode == Libraries.Avalonia.Theming.AvaloniaThemeMode.Light;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(Libraries.Avalonia.Theming.AvaloniaThemeMode.Light);
      }
      OnPropertyChanged(nameof(IsLightModeChecked));
      OnPropertyChanged(nameof(IsDarkModeChecked));
    }
  }
}

using CommunityToolkit.Mvvm.ComponentModel;
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
    get => _themeProvider.CurrentThemeMode == ThemeMode.Dark;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(ThemeMode.Dark);
      }
      OnPropertyChanged(nameof(IsLightModeChecked));
      OnPropertyChanged(nameof(IsDarkModeChecked));
    }
  }

  public bool IsLightModeChecked
  {
    get => _themeProvider.CurrentThemeMode == ThemeMode.Light;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(ThemeMode.Light);
      }
      OnPropertyChanged(nameof(IsLightModeChecked));
      OnPropertyChanged(nameof(IsDarkModeChecked));
    }
  }
}

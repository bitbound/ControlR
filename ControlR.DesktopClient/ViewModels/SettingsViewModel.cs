using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface ISettingsViewModel : IViewModelBase
{
  ThemeMode CurrentThemeMode { get; set; }
}

public partial class SettingsViewModel(IThemeProvider themeProvider) : ViewModelBase<SettingsView>, ISettingsViewModel
{
  private readonly IThemeProvider _themeProvider = themeProvider;

  [ObservableProperty]
  private ThemeMode _currentThemeMode;

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    CurrentThemeMode = _themeProvider.CurrentThemeMode;
  }

  partial void OnCurrentThemeModeChanged(ThemeMode value)
  {
    _themeProvider.SetThemeMode(value);
  }
}

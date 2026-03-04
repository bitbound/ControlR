using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IAppViewModel
{
  IRelayCommand ExitApplicationCommand { get; }
  bool IsDarkMode { get; }
  IRelayCommand ShowMainWindowCommand { get; }
  IRelayCommand ToggleThemeCommand { get; }
}

public partial class AppViewModel : ViewModelBase<App>, IAppViewModel
{
  private readonly IControlledApplicationLifetime _appLifetime;
  private readonly INavigationProvider _navigationProvider;
  private readonly IThemeProvider _themeProvider;

  [ObservableProperty]
  private bool _isDarkMode;

  public AppViewModel(
    IControlledApplicationLifetime appLifetime,
    INavigationProvider navigationProvider,
    IThemeProvider themeProvider)
  {
    _appLifetime = appLifetime;
    _navigationProvider = navigationProvider;
    _themeProvider = themeProvider;

    IsDarkMode = _themeProvider.CurrentTheme == ThemeVariant.Dark;
    _themeProvider.ThemeChanged += OnThemeChanged;
  }

  [RelayCommand]
  private void ExitApplication()
  {
    _appLifetime.Shutdown();
  }

  private void OnThemeChanged(object? sender, EventArgs e)
  {
    IsDarkMode = _themeProvider.CurrentTheme == ThemeVariant.Dark;
  }
  
  [RelayCommand]
  private void ShowMainWindow()
  {
    _navigationProvider.ShowMainWindow();
  }

  [RelayCommand]
  private void ToggleTheme()
  {
    _themeProvider.ToggleTheme();
  }
}
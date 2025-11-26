using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IAppViewModel
{
  ICommand ExitApplicationCommand { get; }
  bool IsDarkMode { get; }
  ICommand ShowWindowCommand { get; }
  ICommand ToggleThemeCommand { get; }
}

public partial class AppViewModel : ViewModelBase, IAppViewModel
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

    ShowWindowCommand = new RelayCommand(ShowMainWindow);
    ExitApplicationCommand = new RelayCommand(() => _appLifetime.Shutdown());
    ToggleThemeCommand = new RelayCommand(ToggleTheme);
  }

  public ICommand ExitApplicationCommand { get; }

  public ICommand ShowWindowCommand { get; }

  public ICommand ToggleThemeCommand { get; }

  private void OnThemeChanged(object? sender, EventArgs e)
  {
    IsDarkMode = _themeProvider.CurrentTheme == ThemeVariant.Dark;
  }

  private void ShowMainWindow()
  {
    _navigationProvider.ShowMainWindow();
  }

  private void ToggleTheme()
  {
    _themeProvider.ToggleTheme();
  }
}
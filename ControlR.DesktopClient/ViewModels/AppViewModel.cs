using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Services;
using ControlR.DesktopClient.Views;

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
  private readonly IClassicDesktopStyleApplicationLifetime _desktop;
  private readonly IServiceProvider _serviceProvider;
  private readonly IThemeProvider _themeProvider;

  [ObservableProperty]
  private bool _isDarkMode;

  public AppViewModel(
    IClassicDesktopStyleApplicationLifetime desktop,
    IServiceProvider serviceProvider,
    IThemeProvider themeProvider)
  {
    _desktop = desktop;
    _serviceProvider = serviceProvider;
    _themeProvider = themeProvider;

    IsDarkMode = _themeProvider.CurrentTheme == ThemeVariant.Dark;
    _themeProvider.ThemeChanged += OnThemeChanged;

    ShowWindowCommand = new RelayCommand(ShowMainWindow);
    ExitApplicationCommand = new RelayCommand(() => _desktop.Shutdown());
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
    _desktop.MainWindow ??= _serviceProvider.GetRequiredService<MainWindow>();
    _desktop.MainWindow.Show();
  }

  private void ToggleTheme()
  {
    _themeProvider.ToggleTheme();
  }
}
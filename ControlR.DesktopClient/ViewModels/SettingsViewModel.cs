using System.Diagnostics;
using ControlR.DesktopClient.Common.ViewModels;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Shared.Services.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.ViewModels;

public interface ISettingsViewModel : IViewModelBase
{
  IReadOnlyList<LocalizationLanguage> AvailableLanguages { get; }
  bool IsDarkModeChecked { get; set; }
  bool IsLightModeChecked { get; set; }
  IRelayCommand RestartClientCommand { get; }
  LocalizationLanguage? SelectedLanguage { get; set; }
}

public partial class SettingsViewModel(
  IThemeProvider themeProvider,
  IDesktopSettingsStore settingsStore,
  IProcessManager processManager,
  IHostApplicationLifetime applicationLifetime,
  ILogger<SettingsViewModel> logger) : ViewModelBase<SettingsView>, ISettingsViewModel
{
  private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
  private readonly ILogger<SettingsViewModel> _logger = logger;
  private readonly IProcessManager _processManager = processManager;
  private readonly IDesktopSettingsStore _settingsStore = settingsStore;
  private readonly IThemeProvider _themeProvider = themeProvider;

  public IReadOnlyList<LocalizationLanguage> AvailableLanguages { get; } = Localization.GetAvailableLanguages();

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

  public LocalizationLanguage? SelectedLanguage
  {
    get => AvailableLanguages.FirstOrDefault(x => x.Culture == _settingsStore.Current.Language);
    set
    {
      if (value is null || value.Culture == _settingsStore.Current.Language)
      {
        return;
      }

      _settingsStore.SetLanguage(value.Culture);
      OnPropertyChanged();
    }
  }

  [RelayCommand]
  private void RestartClient()
  {
    var commandLineArgs = Environment.GetCommandLineArgs();
    var startInfo = new ProcessStartInfo(commandLineArgs[0]);

    foreach (var arg in commandLineArgs[1..])
    {
      startInfo.ArgumentList.Add(arg);
    }

    try
    {
      // Only exit once something is coming back to replace this instance.
      if (_processManager.Start(startInfo) is null)
      {
        _logger.LogError("Restarting the desktop client started no process.");
        return;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unable to restart the desktop client.");
      return;
    }

    _applicationLifetime.StopApplication();
  }
}

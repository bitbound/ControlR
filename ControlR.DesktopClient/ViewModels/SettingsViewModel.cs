using ControlR.DesktopClient.Common.ViewModels;
using ControlR.DesktopClient.Services;
using Microsoft.Extensions.Hosting;

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
  IHostApplicationLifetime applicationLifetime) : ViewModelBase<SettingsView>, ISettingsViewModel
{
  private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
  private readonly IDesktopSettingsStore _settingsStore = settingsStore;
  private readonly IThemeProvider _themeProvider = themeProvider;

  public IReadOnlyList<LocalizationLanguage> AvailableLanguages { get; } = Localization.GetAvailableLanguages();

  public bool IsDarkModeChecked
  {
    get => _themeProvider.CurrentThemeMode == AvaloniaThemeMode.Dark;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(AvaloniaThemeMode.Dark);
      }
      OnPropertyChanged(nameof(IsLightModeChecked));
      OnPropertyChanged(nameof(IsDarkModeChecked));
    }
  }

  public bool IsLightModeChecked
  {
    get => _themeProvider.CurrentThemeMode == AvaloniaThemeMode.Light;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(AvaloniaThemeMode.Light);
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
    // The agent's desktop-client watcher relaunches the client on all three platforms, so a plain
    // exit is the restart. Spawning a replacement here would leave a second client, since
    // AbandonProcessGroup on macOS and KillMode=process on Linux let a child outlive the shutdown.
    _applicationLifetime.StopApplication();
  }
}

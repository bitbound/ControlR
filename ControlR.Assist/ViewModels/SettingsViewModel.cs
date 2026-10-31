using ControlR.Assist.Services;
using ControlR.Libraries.Avalonia.Theming;
using CommunityToolkit.Mvvm.Input;

namespace ControlR.Assist.ViewModels;

public sealed partial class SettingsViewModel(
  IAssistSettingsService settingsService,
  INavigationService navigationService,
  IThemeProvider themeProvider) : ViewModelBase
{
  private readonly INavigationService _navigationService = navigationService;
  private readonly IAssistSettingsService _settingsService = settingsService;
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

      RaiseThemeChecks();
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

      RaiseThemeChecks();
    }
  }

  public bool IsSystemModeChecked
  {
    get => _themeProvider.CurrentThemeMode == AvaloniaThemeMode.System;
    set
    {
      if (value)
      {
        _themeProvider.SetThemeMode(AvaloniaThemeMode.System);
      }

      RaiseThemeChecks();
    }
  }

  /// <summary>
  /// The saved choice, falling back to the culture the strings are already resolved from so the
  /// selector shows the system language when nothing was saved yet.
  /// </summary>
  public LocalizationLanguage? SelectedLanguage
  {
    get
    {
      var culture = _settingsService.GetLanguage() ?? Localization.CurrentCulture;
      return AvailableLanguages.FirstOrDefault(x => string.Equals(x.Culture, culture, StringComparison.OrdinalIgnoreCase));
    }
    set
    {
      if (value is null || string.Equals(value.Culture, _settingsService.GetLanguage(), StringComparison.OrdinalIgnoreCase))
      {
        return;
      }

      _settingsService.SetLanguage(value.Culture);
      OnPropertyChanged();
    }
  }

  [RelayCommand]
  private void Back() => _navigationService.NavigateTo<HomeViewModel>();

  private void RaiseThemeChecks()
  {
    OnPropertyChanged(nameof(IsDarkModeChecked));
    OnPropertyChanged(nameof(IsLightModeChecked));
    OnPropertyChanged(nameof(IsSystemModeChecked));
  }
}

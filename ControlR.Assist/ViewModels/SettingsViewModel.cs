using ControlR.Assist.Services;
using ControlR.Libraries.Avalonia.Theming;
using ControlR.Libraries.Shared.Services.Processes;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.ViewModels;

public sealed partial class SettingsViewModel(
  IAssistSettingsService settingsService,
  ILogger<SettingsViewModel> logger,
  IProcessManager processManager,
  INavigationService navigationService,
  IThemeProvider themeProvider) : ViewModelBase
{
  private readonly ILogger<SettingsViewModel> _logger = logger;
  private readonly INavigationService _navigationService = navigationService;
  private readonly IProcessManager _processManager = processManager;
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
  /// Set when the file explorer could not be opened.
  /// </summary>
  public string? LogFolderError { get; private set; }

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

#if DEBUG
  public bool ShowLogFolder => true;
#else
  public bool ShowLogFolder => false;
#endif

  [RelayCommand]
  private void Back() => _navigationService.NavigateTo<HomeViewModel>();

  [RelayCommand]
  private void OpenLogFolder()
  {
    LogFolderError = null;

    try
    {
      _ = _processManager.Start(AssistSettingsService.AppDataDirectoryPath, string.Empty, true);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to open the log folder.");
      LogFolderError = Localization.StatusLogFolderFailed;
    }

    OnPropertyChanged(nameof(LogFolderError));
  }

  private void RaiseThemeChecks()
  {
    OnPropertyChanged(nameof(IsDarkModeChecked));
    OnPropertyChanged(nameof(IsLightModeChecked));
    OnPropertyChanged(nameof(IsSystemModeChecked));
  }
}

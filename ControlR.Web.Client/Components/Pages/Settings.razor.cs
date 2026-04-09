using ControlR.Web.Client.DataValidation;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Components.Pages;

public partial class Settings
{
  private double _autoQualityLowerThresholdMbps = AppConstants.DefaultRemoteControlAutoQualityLowerThresholdMbps;
  private int _autoQualityMaximum = AppConstants.DefaultRemoteControlAutoQualityMaximum;
  private int _autoQualityMinimum = AppConstants.DefaultRemoteControlAutoQualityMinimum;
  private double _autoQualityUpperThresholdMbps = AppConstants.DefaultRemoteControlAutoQualityUpperThresholdMbps;
  private bool _captureCursor = AppConstants.DefaultRemoteControlCaptureCursor;
  private bool _isAutoQualityEnabled = AppConstants.DefaultRemoteControlIsAutoQualityEnabled;
  private bool _isMaxBandwidthEnabled = AppConstants.DefaultRemoteControlIsMaxBandwidthEnabled;
  private bool _isNotifyUserEnforced;
  private KeyboardInputMode _keyboardInputMode = KeyboardInputMode.Auto;
  private int _manualQuality = AppConstants.DefaultRemoteControlManualQuality;
  private double _maxBandwidthMbps = AppConstants.DefaultRemoteControlMaxBandwidthMbps;
  private bool _notifyUser;
  private Guid? _tenantId;
  private ThemeMode _themeMode = ThemeMode.Auto;
  private string _userDisplayName = string.Empty;
  private Guid? _userId;
  private ViewMode _viewMode = ViewMode.Fit;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }
  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }
  [Inject]
  public required IEffectiveUserPreferences EffectiveUserPreferences { get; init; }
  [Inject]
  public required IJsInterop JsInterop { get; init; }
  [Inject]
  public required ILogger<Settings> Logger { get; init; }
  [Inject]
  public required IMessenger Messenger { get; init; }
  [Inject]
  public required ISnackbar Snackbar { get; init; }
  [Inject]
  public required IUserPreferencesProvider UserPreferences { get; init; }

  protected override async Task OnInitializedAsync()
  {
    var state = await AuthState.GetAuthenticationStateAsync();
    if (state.User.TryGetTenantId(out var tenantId))
    {
      _tenantId = tenantId;
    }

    if (state.User.TryGetUserId(out var userId))
    {
      _userId = userId;
    }

    var notifyUserPreference = await EffectiveUserPreferences.GetNotifyUserOnSessionStart();
    var preferences = await UserPreferences.GetPreferences();
    _notifyUser = notifyUserPreference.Value;
    _isNotifyUserEnforced = notifyUserPreference.IsTenantEnforced;
    _userDisplayName = preferences.UserDisplayName;
    _themeMode = preferences.ThemeMode.ToClientThemeMode();
    _keyboardInputMode = preferences.KeyboardInputMode;
    _viewMode = preferences.ViewMode.ToClientViewMode();
    _captureCursor = preferences.CaptureCursor;
    _isAutoQualityEnabled = preferences.IsAutoQualityEnabled;
    _manualQuality = Math.Clamp(preferences.ManualQuality, 1, 100);
    _autoQualityLowerThresholdMbps = Math.Max(0.1d, preferences.AutoQualityLowerThresholdMbps);
    _autoQualityMaximum = Math.Clamp(preferences.AutoQualityMaximum, 2, 100);
    _autoQualityMinimum = Math.Clamp(preferences.AutoQualityMinimum, 1, 99);

    if (_autoQualityMaximum <= _autoQualityMinimum)
    {
      _autoQualityMaximum = _autoQualityMinimum + 1;
    }

    _autoQualityUpperThresholdMbps = Math.Max(
      _autoQualityLowerThresholdMbps + 0.1d,
      preferences.AutoQualityUpperThresholdMbps);
    _isMaxBandwidthEnabled = preferences.IsMaxBandwidthEnabled;
    _maxBandwidthMbps = Math.Max(0.1d, preferences.MaxBandwidthMbps);

    await base.OnInitializedAsync();
  }

  private static string? ValidateDisplayName(string input)
  {
    if (string.IsNullOrEmpty(input))
    {
      return null;
    }

    if (input.Length > 25)
    {
      return "Display name must be 25 characters or less.";
    }

    return Validators.DisplayNameValidator().IsMatch(input)
      ? "Display name can only contain letters, numbers, underscores, hyphens, and spaces."
      : null;
  }

  private async Task CopyTenantId()
  {
    await ClipboardManager.SetText($"{_tenantId}");
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task CopyUserId()
  {
    await ClipboardManager.SetText($"{_userId}");
    Snackbar.Add("Copied to clipboard", Severity.Success);
  }

  private async Task RestoreQualityDefaults()
  {
    _autoQualityLowerThresholdMbps = AppConstants.DefaultRemoteControlAutoQualityLowerThresholdMbps;
    _autoQualityMaximum = AppConstants.DefaultRemoteControlAutoQualityMaximum;
    _autoQualityMinimum = AppConstants.DefaultRemoteControlAutoQualityMinimum;
    _autoQualityUpperThresholdMbps = AppConstants.DefaultRemoteControlAutoQualityUpperThresholdMbps;
    _manualQuality = AppConstants.DefaultRemoteControlManualQuality;
    _maxBandwidthMbps = AppConstants.DefaultRemoteControlMaxBandwidthMbps;

    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityLowerThresholdMbps, _autoQualityLowerThresholdMbps);
    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityMaximum, _autoQualityMaximum);
    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityMinimum, _autoQualityMinimum);
    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityUpperThresholdMbps, _autoQualityUpperThresholdMbps);
    await UserPreferences.SetPreference(UserPreferenceNames.ManualQuality, _manualQuality);
    await UserPreferences.SetPreference(UserPreferenceNames.MaxBandwidthMbps, _maxBandwidthMbps);

    Snackbar.Add("Quality settings restored to defaults", Severity.Success);
  }

  private async Task SetAutoQualityLowerThresholdMbps(double value)
  {
    _autoQualityLowerThresholdMbps = Math.Max(value, 1);

    if (_autoQualityUpperThresholdMbps <= _autoQualityLowerThresholdMbps)
    {
      _autoQualityUpperThresholdMbps = _autoQualityLowerThresholdMbps + 1;
      await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityUpperThresholdMbps, _autoQualityUpperThresholdMbps);
    }

    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityLowerThresholdMbps, _autoQualityLowerThresholdMbps);
    Snackbar.Add("Auto quality lower threshold updated", Severity.Success);
    await InvokeAsync(StateHasChanged);
  }

  private async Task SetAutoQualityMaximum(int value)
  {
    _autoQualityMaximum = Math.Clamp(value, 2, 100);

    if (_autoQualityMinimum >= _autoQualityMaximum)
    {
      _autoQualityMinimum = _autoQualityMaximum - 1;
      await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityMinimum, _autoQualityMinimum);
    }

    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityMaximum, _autoQualityMaximum);
    Snackbar.Add("Auto quality maximum updated", Severity.Success);
    await InvokeAsync(StateHasChanged);
  }

  private async Task SetAutoQualityMinimum(int value)
  {
    _autoQualityMinimum = Math.Clamp(value, 1, 99);

    if (_autoQualityMaximum <= _autoQualityMinimum)
    {
      _autoQualityMaximum = _autoQualityMinimum + 1;
      await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityMaximum, _autoQualityMaximum);
    }

    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityMinimum, _autoQualityMinimum);
    Snackbar.Add("Auto quality minimum updated", Severity.Success);
    await InvokeAsync(StateHasChanged);
  }

  private async Task SetAutoQualityUpperThresholdMbps(double value)
  {
    _autoQualityUpperThresholdMbps = Math.Max(value, 2);

    if (_autoQualityUpperThresholdMbps <= _autoQualityLowerThresholdMbps)
    {
      _autoQualityLowerThresholdMbps = _autoQualityUpperThresholdMbps - 1;
      await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityLowerThresholdMbps, _autoQualityLowerThresholdMbps);
    }

    await UserPreferences.SetPreference(UserPreferenceNames.AutoQualityUpperThresholdMbps, _autoQualityUpperThresholdMbps);
    Snackbar.Add("Auto quality upper threshold updated", Severity.Success);
    await InvokeAsync(StateHasChanged);
  }

  private async Task SetCaptureCursor(bool value)
  {
    _captureCursor = value;
    await UserPreferences.SetPreference(UserPreferenceNames.CaptureCursor, value);
    Snackbar.Add("Default capture cursor updated", Severity.Success);
  }

  private async Task SetIsAutoQualityEnabled(bool value)
  {
    _isAutoQualityEnabled = value;
    await UserPreferences.SetPreference(UserPreferenceNames.IsAutoQualityEnabled, value);
    Snackbar.Add("Default auto quality updated", Severity.Success);
  }

  private async Task SetIsMaxBandwidthEnabled(bool value)
  {
    _isMaxBandwidthEnabled = value;
    await UserPreferences.SetPreference(UserPreferenceNames.IsMaxBandwidthEnabled, value);
    Snackbar.Add("Default max bandwidth updated", Severity.Success);
  }

  private async Task SetKeyboardInputMode(KeyboardInputMode value)
  {
    _keyboardInputMode = value;
    await UserPreferences.SetPreference(UserPreferenceNames.KeyboardInputMode, value);
    Snackbar.Add("Keyboard input mode updated", Severity.Success);
  }

  private async Task SetManualQuality(int value)
  {
    _manualQuality = Math.Clamp(value, 1, 100);
    await UserPreferences.SetPreference(UserPreferenceNames.ManualQuality, _manualQuality);
    Snackbar.Add("Manual quality updated", Severity.Success);
  }

  private async Task SetMaxBandwidthMbps(double value)
  {
    _maxBandwidthMbps = Math.Max(0.1d, Math.Round(value, 2));
    await UserPreferences.SetPreference(UserPreferenceNames.MaxBandwidthMbps, _maxBandwidthMbps);
    Snackbar.Add("Max bandwidth updated", Severity.Success);
  }

  private async Task SetNotifyUser(bool value)
  {
    if (_isNotifyUserEnforced)
    {
      return;
    }

    _notifyUser = value;
    await UserPreferences.SetPreference(UserPreferenceNames.NotifyUserOnSessionStart, value);
    Snackbar.Add("Notify user on session start updated", Severity.Success);
  }

  private async Task SetThemeMode(ThemeMode value)
  {
    _themeMode = value;
    await UserPreferences.SetPreference(UserPreferenceNames.ThemeMode, value);
    await Messenger.Send(new ThemeChangedMessage(value));
    Snackbar.Add("Theme updated", Severity.Success);
  }

  private async Task SetUserDisplayName(string value)
  {
    _userDisplayName = value;
    await UserPreferences.SetPreference(UserPreferenceNames.UserDisplayName, value);
    Snackbar.Add("Display name updated", Severity.Success);
  }

  private async Task SetViewMode(ViewMode value)
  {
    _viewMode = value;
    await UserPreferences.SetPreference(UserPreferenceNames.ViewMode, value);
    Snackbar.Add("View mode updated", Severity.Success);
  }
}
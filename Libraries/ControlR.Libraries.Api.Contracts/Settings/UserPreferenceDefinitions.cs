using System.Collections.Frozen;
using System.Globalization;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Settings;

public static class UserPreferenceDefinitions
{
  public const double DefaultAutoQualityLowerThresholdMbps = 5d;
  public const int DefaultAutoQualityMaximum = 80;
  public const int DefaultAutoQualityMinimum = 20;
  public const double DefaultAutoQualityUpperThresholdMbps = 15d;
  public const bool DefaultCaptureCursor = false;
  public const bool DefaultEnableDirectX = true;
  public const ImageFormat DefaultEncodingFormat = ImageFormat.Jpeg;
  public const bool DefaultHideOfflineDevices = true;
  public const bool DefaultIsAutoQualityEnabled = false;
  public const bool DefaultIsMaxBandwidthEnabled = false;
  public const int DefaultManualQuality = 75;
  public const double DefaultMaxBandwidthMbps = 15d;
  public const bool DefaultNotifyUserOnSessionStart = true;
  public const bool DefaultOpenDeviceInNewTab = true;
  public const bool DefaultShowOnlyUngroupedDevices = false;
  public const bool DefaultShowOnlyUntaggedDevices = false;

  /// <summary>
  /// Every preference, in <see cref="InternalDtos.UserPreferencesDto"/> constructor order.
  /// </summary>
  public static IReadOnlyList<ISettingDefinition> All =>
  [
    AutoQualityLowerThresholdMbps,
    AutoQualityMaximum,
    AutoQualityMinimum,
    AutoQualityUpperThresholdMbps,
    CaptureCursor,
    EncodingFormat,
    EnableDirectX,
    HideOfflineDevices,
    ShowOnlyUntaggedDevices,
    ShowOnlyUngroupedDevices,
    IsAutoQualityEnabled,
    IsMaxBandwidthEnabled,
    KeyboardInputMode,
    ManualQuality,
    MaxBandwidthMbps,
    NotifyUserOnSessionStart,
    OpenDeviceInNewTab,
    ThemeMode,
    UserDisplayName,
    ViewMode
  ];
  public static SettingDefinition<double> AutoQualityLowerThresholdMbps { get; } =
    SettingDefinition.CreateDouble(UserPreferenceNames.AutoQualityLowerThresholdMbps, DefaultAutoQualityLowerThresholdMbps, 0.1d);
  public static SettingDefinition<int> AutoQualityMaximum { get; } =
    SettingDefinition.CreateInt(UserPreferenceNames.AutoQualityMaximum, DefaultAutoQualityMaximum, 2, 100);
  public static SettingDefinition<int> AutoQualityMinimum { get; } =
    SettingDefinition.CreateInt(UserPreferenceNames.AutoQualityMinimum, DefaultAutoQualityMinimum, 1, 99);
  public static SettingDefinition<double> AutoQualityUpperThresholdMbps { get; } =
    SettingDefinition.CreateDouble(UserPreferenceNames.AutoQualityUpperThresholdMbps, DefaultAutoQualityUpperThresholdMbps, 0.1d);
  public static SettingDefinition<bool> CaptureCursor { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.CaptureCursor, DefaultCaptureCursor);
  public static SettingDefinition<bool> EnableDirectX { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.EnableDirectX, DefaultEnableDirectX);
  public static SettingDefinition<ImageFormat> EncodingFormat { get; } =
    SettingDefinition.CreateEnum(UserPreferenceNames.EncodingFormat, DefaultEncodingFormat);
  public static SettingDefinition<bool> HideOfflineDevices { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.HideOfflineDevices, DefaultHideOfflineDevices);
  public static SettingDefinition<bool> IsAutoQualityEnabled { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.IsAutoQualityEnabled, DefaultIsAutoQualityEnabled);
  public static SettingDefinition<bool> IsMaxBandwidthEnabled { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.IsMaxBandwidthEnabled, DefaultIsMaxBandwidthEnabled);
  public static SettingDefinition<KeyboardInputMode> KeyboardInputMode { get; } =
    SettingDefinition.CreateEnum(UserPreferenceNames.KeyboardInputMode, Enums.KeyboardInputMode.Auto);
  public static SettingDefinition<int> ManualQuality { get; } =
    SettingDefinition.CreateInt(UserPreferenceNames.ManualQuality, DefaultManualQuality, 1, 100);
  public static SettingDefinition<double> MaxBandwidthMbps { get; } =
    SettingDefinition.CreateDouble(UserPreferenceNames.MaxBandwidthMbps, DefaultMaxBandwidthMbps, 0.1d);
  public static SettingDefinition<bool> NotifyUserOnSessionStart { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.NotifyUserOnSessionStart, DefaultNotifyUserOnSessionStart);
  public static SettingDefinition<bool> OpenDeviceInNewTab { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.OpenDeviceInNewTab, DefaultOpenDeviceInNewTab);
  public static SettingDefinition<bool> ShowOnlyUngroupedDevices { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.ShowOnlyUngroupedDevices, DefaultShowOnlyUngroupedDevices);
  public static SettingDefinition<bool> ShowOnlyUntaggedDevices { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.ShowOnlyUntaggedDevices, DefaultShowOnlyUntaggedDevices);
  public static SettingDefinition<ThemeMode> ThemeMode { get; } =
    SettingDefinition.CreateEnum(UserPreferenceNames.ThemeMode, Enums.ThemeMode.Dark);
  public static SettingDefinition<string> UserDisplayName { get; } =
    new(
      UserPreferenceNames.UserDisplayName,
      string.Empty,
      value => ParseResult<string>.Success(value.Trim()),
      validate: ValidateUserDisplayName,
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid display name.");
  public static SettingDefinition<ViewMode> ViewMode { get; } =
    SettingDefinition.CreateEnum(UserPreferenceNames.ViewMode, Enums.ViewMode.Fit);

  private static FrozenDictionary<string, ISettingDefinition> DefinitionsByName { get; } =
    All.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

  public static UserPreferencesDto CreateDto(
    IReadOnlyDictionary<string, string> values,
    Action<string, string>? onInvalidValue = null)
  {
    TValue Read<TValue>(SettingDefinition<TValue> definition)
    {
      return definition.ReadValue(values, value => onInvalidValue?.Invoke(definition.Name, value));
    }

    return new UserPreferencesDto(
      Read(AutoQualityLowerThresholdMbps),
      Read(AutoQualityMaximum),
      Read(AutoQualityMinimum),
      Read(AutoQualityUpperThresholdMbps),
      Read(CaptureCursor),
      Read(EncodingFormat),
      Read(EnableDirectX),
      Read(HideOfflineDevices),
      Read(ShowOnlyUntaggedDevices),
      Read(ShowOnlyUngroupedDevices),
      Read(IsAutoQualityEnabled),
      Read(IsMaxBandwidthEnabled),
      Read(KeyboardInputMode),
      Read(ManualQuality),
      Read(MaxBandwidthMbps),
      Read(NotifyUserOnSessionStart),
      Read(OpenDeviceInNewTab),
      Read(ThemeMode),
      Read(UserDisplayName),
      Read(ViewMode));
  }

  public static string? FormatValue(string name, object? value)
  {
    if (DefinitionsByName.TryGetValue(name, out var definition))
    {
      return definition.FormatObjectValue(value);
    }

    return value switch
    {
      null => null,
      IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
      _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
  }

  public static IReadOnlyList<(string Name, string? Value)> GetValues(UserPreferencesDto preferences)
  {
    return
    [
      (AutoQualityLowerThresholdMbps.Name, AutoQualityLowerThresholdMbps.FormatValue(preferences.AutoQualityLowerThresholdMbps)),
      (AutoQualityMaximum.Name, AutoQualityMaximum.FormatValue(preferences.AutoQualityMaximum)),
      (AutoQualityMinimum.Name, AutoQualityMinimum.FormatValue(preferences.AutoQualityMinimum)),
      (AutoQualityUpperThresholdMbps.Name, AutoQualityUpperThresholdMbps.FormatValue(preferences.AutoQualityUpperThresholdMbps)),
      (CaptureCursor.Name, CaptureCursor.FormatValue(preferences.CaptureCursor)),
      (EncodingFormat.Name, EncodingFormat.FormatValue(preferences.EncodingFormat)),
      (EnableDirectX.Name, EnableDirectX.FormatValue(preferences.EnableDirectX)),
      (HideOfflineDevices.Name, HideOfflineDevices.FormatValue(preferences.HideOfflineDevices)),
      (ShowOnlyUntaggedDevices.Name, ShowOnlyUntaggedDevices.FormatValue(preferences.ShowOnlyUntaggedDevices)),
      (ShowOnlyUngroupedDevices.Name, ShowOnlyUngroupedDevices.FormatValue(preferences.ShowOnlyUngroupedDevices)),
      (IsAutoQualityEnabled.Name, IsAutoQualityEnabled.FormatValue(preferences.IsAutoQualityEnabled)),
      (IsMaxBandwidthEnabled.Name, IsMaxBandwidthEnabled.FormatValue(preferences.IsMaxBandwidthEnabled)),
      (KeyboardInputMode.Name, KeyboardInputMode.FormatValue(preferences.KeyboardInputMode)),
      (ManualQuality.Name, ManualQuality.FormatValue(preferences.ManualQuality)),
      (MaxBandwidthMbps.Name, MaxBandwidthMbps.FormatValue(preferences.MaxBandwidthMbps)),
      (NotifyUserOnSessionStart.Name, NotifyUserOnSessionStart.FormatValue(preferences.NotifyUserOnSessionStart)),
      (OpenDeviceInNewTab.Name, OpenDeviceInNewTab.FormatValue(preferences.OpenDeviceInNewTab)),
      (ThemeMode.Name, ThemeMode.FormatValue(preferences.ThemeMode)),
      (UserDisplayName.Name, UserDisplayName.FormatValue(preferences.UserDisplayName)),
      (ViewMode.Name, ViewMode.FormatValue(preferences.ViewMode))
    ];
  }

  public static SettingValueNormalizationResult Normalize(string name, string value)
  {
    if (DefinitionsByName.TryGetValue(name, out var definition))
    {
      return definition.Normalize(value);
    }

    return SettingValueNormalizationResult.Success(value.Trim());
  }

  private static string? ValidateUserDisplayName(string value)
  {
    if (value.Length > 50)
    {
      return "User display name must be 50 characters or less.";
    }

    var illegalCharacters = value
      .Where(c => !char.IsLetterOrDigit(c) && c is not ' ' and not '_' and not '-')
      .Distinct()
      .ToArray();

    if (illegalCharacters.Length == 0)
    {
      return null;
    }

    return $"User display name can only contain letters, numbers, underscores, hyphens, and spaces. Invalid characters: {string.Join(", ", illegalCharacters)}";
  }
}

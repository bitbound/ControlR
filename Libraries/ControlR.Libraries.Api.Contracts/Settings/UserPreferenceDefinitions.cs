using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Settings;

public static class UserPreferenceDefinitions
{
  public const double DefaultAutoQualityLowerThresholdMbps = 5d;
  public const int DefaultAutoQualityMaximum = 80;
  public const int DefaultAutoQualityMinimum = 20;
  public const double DefaultAutoQualityUpperThresholdMbps = 15d;
  public const bool DefaultCaptureCursor = false;
  public const bool DefaultEnableDirectX = true;
  public const bool DefaultHideOfflineDevices = true;
  public const bool DefaultIncludeUntaggedDevices = false;
  public const bool DefaultIsAutoQualityEnabled = false;
  public const bool DefaultIsMaxBandwidthEnabled = false;
  public const int DefaultManualQuality = 75;
  public const double DefaultMaxBandwidthMbps = 15d;
  public const bool DefaultNotifyUserOnSessionStart = true;
  public const bool DefaultOpenDeviceInNewTab = true;

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
  public static SettingDefinition<bool> HideOfflineDevices { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.HideOfflineDevices, DefaultHideOfflineDevices);
  public static SettingDefinition<bool> IncludeUntaggedDevices { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.IncludeUntaggedDevices, DefaultIncludeUntaggedDevices);
  public static SettingDefinition<bool> IsAutoQualityEnabled { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.IsAutoQualityEnabled, DefaultIsAutoQualityEnabled);
  public static SettingDefinition<bool> IsMaxBandwidthEnabled { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.IsMaxBandwidthEnabled, DefaultIsMaxBandwidthEnabled);
  public static SettingDefinition<KeyboardInputMode> KeyboardInputMode { get; } =
    SettingDefinition.CreateEnum(UserPreferenceNames.KeyboardInputMode, ControlR.Libraries.Api.Contracts.Enums.KeyboardInputMode.Auto);
  public static SettingDefinition<int> ManualQuality { get; } =
    SettingDefinition.CreateInt(UserPreferenceNames.ManualQuality, DefaultManualQuality, 1, 100);
  public static SettingDefinition<double> MaxBandwidthMbps { get; } =
    SettingDefinition.CreateDouble(UserPreferenceNames.MaxBandwidthMbps, DefaultMaxBandwidthMbps, 0.1d);
  public static SettingDefinition<bool> NotifyUserOnSessionStart { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.NotifyUserOnSessionStart, DefaultNotifyUserOnSessionStart);
  public static SettingDefinition<bool> OpenDeviceInNewTab { get; } =
    SettingDefinition.CreateBoolean(UserPreferenceNames.OpenDeviceInNewTab, DefaultOpenDeviceInNewTab);
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

  public static UserPreferencesDto CreateDto(
    IReadOnlyDictionary<string, string> values,
    Action<string, string>? onInvalidValue = null)
  {
    return SettingsDtoMapper.CreateDto<UserPreferencesDto>(Cache.DefinitionsByPropertyName, values, onInvalidValue);
  }

  public static string? FormatValue(string name, object? value)
  {
    if (Cache.DefinitionsBySettingName.TryGetValue(name, out var definition))
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
    return SettingsDtoMapper.GetValues(Cache.DefinitionsByPropertyName, preferences);
  }

  public static SettingValueNormalizationResult Normalize(string name, string value)
  {
    if (Cache.DefinitionsBySettingName.TryGetValue(name, out var definition))
    {
      return definition.Normalize(value);
    }

    return SettingValueNormalizationResult.Success(value.Trim());
  }

  private static FrozenDictionary<string, ISettingDefinition> GetDefinitionsByPropertyName()
  {
    return typeof(UserPreferenceDefinitions)
      .GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(x => typeof(ISettingDefinition).IsAssignableFrom(x.PropertyType))
      .ToFrozenDictionary(
        x => x.Name, 
        x => (ISettingDefinition)(x.GetValue(null) 
          ?? throw new InvalidOperationException($"Definition {x.Name} is null.")), StringComparer.Ordinal);
  }

  private static string? ValidateUserDisplayName(string value)
  {
    if (value.Length > 25)
    {
      return "User display name must be 25 characters or less.";
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

  private static class Cache
  {
    internal static readonly FrozenDictionary<string, ISettingDefinition> DefinitionsByPropertyName = GetDefinitionsByPropertyName();
    internal static readonly FrozenDictionary<string, ISettingDefinition> DefinitionsBySettingName = DefinitionsByPropertyName
      .Values
      .ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);
  }
}
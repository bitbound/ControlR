using System.Collections.Frozen;
using System.Globalization;
using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Settings;

public static class TenantSettingDefinitions
{

  /// <summary>
  /// Every setting, in <see cref="InternalDtos.TenantSettingsDto"/> constructor order.
  /// </summary>
  public static IReadOnlyList<ISettingDefinition> All =>
  [
    AppendInstanceId,
    InstanceId,
    NotifyUserOnSessionStart
  ];
  public static SettingDefinition<bool?> AppendInstanceId { get; } =
    new(
      TenantSettingNames.AppendInstanceId,
      null,
      value => bool.TryParse(value, out var parsedValue)
        ? ParseResult<bool?>.Success(parsedValue)
        : ParseResult<bool?>.Failure(null),
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid boolean value.");
  public static SettingDefinition<string?> InstanceId { get; } =
    new(
      TenantSettingNames.InstanceId,
      null,
      value => ParseResult<string?>.Success(string.IsNullOrWhiteSpace(value) ? null : value.Trim()),
      validate: ValidateInstanceId,
      invalidValueMessageFactory: settingName => $"{settingName} has an invalid value.");
  public static SettingDefinition<bool?> NotifyUserOnSessionStart { get; } =
    new(
      TenantSettingNames.NotifyUserOnSessionStart,
      null,
      value => bool.TryParse(value, out var parsedValue)
        ? ParseResult<bool?>.Success(parsedValue)
        : ParseResult<bool?>.Failure(null),
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid boolean value.");

  private static FrozenDictionary<string, ISettingDefinition> DefinitionsByName { get; } =
    All.ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

  public static TenantSettingsDto CreateDto(
    IReadOnlyDictionary<string, string> values,
    Action<string, string>? onInvalidValue = null)
  {
    TValue Read<TValue>(SettingDefinition<TValue> definition)
    {
      return definition.ReadValue(values, value => onInvalidValue?.Invoke(definition.Name, value));
    }

    return new TenantSettingsDto(
      Read(AppendInstanceId),
      Read(InstanceId),
      Read(NotifyUserOnSessionStart));
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

  public static IReadOnlyList<(string Name, string? Value)> GetValues(TenantSettingsDto settings)
  {
    return
    [
      (AppendInstanceId.Name, AppendInstanceId.FormatValue(settings.AppendInstanceId)),
      (InstanceId.Name, InstanceId.FormatValue(settings.InstanceId)),
      (NotifyUserOnSessionStart.Name, NotifyUserOnSessionStart.FormatValue(settings.NotifyUserOnSessionStart))
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

  private static string? ValidateInstanceId(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    var trimmedValue = value.Trim();
    if (string.Equals(trimmedValue, "default", StringComparison.OrdinalIgnoreCase))
    {
      return "Instance ID 'default' is reserved.";
    }

    if (trimmedValue is "." or "..")
    {
      return "Instance ID cannot be '.' or '..'.";
    }

    if (trimmedValue.Contains(Path.DirectorySeparatorChar) || trimmedValue.Contains(Path.AltDirectorySeparatorChar))
    {
      return "Instance ID must not contain path separators.";
    }

    var invalidCharacters = trimmedValue
      .Where(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-')
      .Distinct()
      .ToArray();

    if (invalidCharacters.Length == 0)
    {
      return null;
    }

    return $"Instance ID contains one or more invalid characters: {string.Join(", ", invalidCharacters)}";
  }
}

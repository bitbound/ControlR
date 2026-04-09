using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

namespace ControlR.Libraries.Api.Contracts.Settings;

public static class TenantSettingDefinitions
{
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

  public static TenantSettingsDto CreateDto(
    IReadOnlyDictionary<string, string> values,
    Action<string, string>? onInvalidValue = null)
  {
    return SettingsDtoMapper.CreateDto<TenantSettingsDto>(Cache.DefinitionsByPropertyName, values, onInvalidValue);
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

  public static IReadOnlyList<(string Name, string? Value)> GetValues(TenantSettingsDto settings)
  {
    return SettingsDtoMapper.GetValues(Cache.DefinitionsByPropertyName, settings);
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
    return typeof(TenantSettingDefinitions)
      .GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(x => typeof(ISettingDefinition).IsAssignableFrom(x.PropertyType))
      .ToFrozenDictionary(
        x => x.Name, 
        x => (ISettingDefinition)(x.GetValue(null) 
          ?? throw new InvalidOperationException($"Definition {x.Name} is null.")), StringComparer.Ordinal);
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

  private static class Cache
  {
    internal static readonly FrozenDictionary<string, ISettingDefinition> DefinitionsByPropertyName = GetDefinitionsByPropertyName();
    internal static readonly FrozenDictionary<string, ISettingDefinition> DefinitionsBySettingName = DefinitionsByPropertyName
      .Values
      .ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);
  }
}
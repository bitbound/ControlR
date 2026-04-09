using System.Collections.Frozen;
using System.Reflection;

namespace ControlR.Libraries.Api.Contracts.Settings;

internal static class SettingsDtoMapper
{
  public static TDto CreateDto<TDto>(
    FrozenDictionary<string, ISettingDefinition> definitionsByPropertyName,
    IReadOnlyDictionary<string, string> values,
    Action<string, string>? onInvalidValue = null)
  {
    return SettingsDtoCache<TDto>.Create(definitionsByPropertyName, values, onInvalidValue);
  }

  public static IReadOnlyList<(string Name, string? Value)> GetValues<TDto>(
    FrozenDictionary<string, ISettingDefinition> definitionsByPropertyName,
    TDto dto)
  {
    return SettingsDtoCache<TDto>.GetValues(definitionsByPropertyName, dto);
  }

  private static class SettingsDtoCache<TDto>
  {
    private static readonly ConstructorInfo _constructor = typeof(TDto)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
      .OrderByDescending(x => x.GetParameters().Length)
      .FirstOrDefault()
      ?? throw new InvalidOperationException($"Type {typeof(TDto).Name} must have a public constructor.");

    private static readonly FrozenDictionary<string, PropertyInfo> _properties = typeof(TDto)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Where(x => x.CanRead)
      .ToFrozenDictionary(x => x.Name, StringComparer.Ordinal);

    public static TDto Create(
      FrozenDictionary<string, ISettingDefinition> definitionsByPropertyName,
      IReadOnlyDictionary<string, string> values,
      Action<string, string>? onInvalidValue)
    {
      var arguments = _constructor
        .GetParameters()
        .Select(parameter => ReadValue(definitionsByPropertyName, values, parameter.Name, onInvalidValue))
        .ToArray();

      return (TDto)_constructor.Invoke(arguments);
    }

    public static IReadOnlyList<(string Name, string? Value)> GetValues(
      FrozenDictionary<string, ISettingDefinition> definitionsByPropertyName,
      TDto dto)
    {
      return
      [
        .. definitionsByPropertyName.Select(x =>
        {
          var property = GetProperty(x.Key);
          var rawValue = property.GetValue(dto);
          return (x.Value.Name, x.Value.FormatObjectValue(rawValue));
        })
      ];
    }

    private static PropertyInfo GetProperty(string propertyName)
    {
      if (_properties.TryGetValue(propertyName, out var property))
      {
        return property;
      }

      throw new InvalidOperationException($"Type {typeof(TDto).Name} is missing property {propertyName}.");
    }

    private static object? ReadValue(
      FrozenDictionary<string, ISettingDefinition> definitionsByPropertyName,
      IReadOnlyDictionary<string, string> values,
      string? propertyName,
      Action<string, string>? onInvalidValue)
    {
      if (string.IsNullOrWhiteSpace(propertyName))
      {
        throw new InvalidOperationException($"Type {typeof(TDto).Name} contains a constructor parameter without a name.");
      }

      if (definitionsByPropertyName.TryGetValue(propertyName, out var definition))
      {
        return definition.ReadObjectValue(values, value => onInvalidValue?.Invoke(definition.Name, value));
      }

      throw new InvalidOperationException($"No setting definition exists for {typeof(TDto).Name}.{propertyName}.");
    }
  }
}
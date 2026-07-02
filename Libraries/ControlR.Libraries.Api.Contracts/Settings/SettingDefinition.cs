using System.Globalization;

namespace ControlR.Libraries.Api.Contracts.Settings;

public interface ISettingDefinition
{
  string Name { get; }

  string? FormatObjectValue(object? value);

  SettingValueNormalizationResult Normalize(string value);

  object? ReadObjectValue(IReadOnlyDictionary<string, string> values, Action<string>? onInvalidValue = null);
}

public sealed class SettingDefinition<T>(
  string name,
  T defaultValue,
  Func<string, ParseResult<T>> parse,
  Func<T, string?>? validate = null,
  Func<string, string>? invalidValueMessageFactory = null) : ISettingDefinition
{
  public T DefaultValue { get; } = defaultValue;
  public string Name { get; } = name;

  public string? FormatObjectValue(object? value)
  {
    if (value is null)
    {
      return null;
    }

    if (value is T typedValue)
    {
      return FormatValue(typedValue);
    }

    throw new InvalidOperationException($"Value for {Name} must be assignable to {typeof(T).Name}.");
  }

  public string? FormatValue(T value)
  {
    return value switch
    {
      null => null,
      string stringValue => stringValue,
      IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
      _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
  }

  public SettingValueNormalizationResult Normalize(string value)
  {
    var result = parse(value);
    if (!result.IsSuccess)
    {
      return SettingValueNormalizationResult.Failure(
        invalidValueMessageFactory?.Invoke(Name) ?? $"{Name} has an invalid value.");
    }

    var validationError = validate?.Invoke(result.Value);
    if (!string.IsNullOrWhiteSpace(validationError))
    {
      return SettingValueNormalizationResult.Failure(validationError);
    }

    return SettingValueNormalizationResult.Success(FormatValue(result.Value));
  }

  public object? ReadObjectValue(IReadOnlyDictionary<string, string> values, Action<string>? onInvalidValue = null)
  {
    return ReadValue(values, onInvalidValue);
  }

  public T ReadValue(IReadOnlyDictionary<string, string> values, Action<string>? onInvalidValue = null)
  {
    if (!values.TryGetValue(Name, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
    {
      return DefaultValue;
    }

    var result = parse(rawValue);
    if (result.IsSuccess)
    {
      return result.Value;
    }

    onInvalidValue?.Invoke(rawValue);
    return DefaultValue;
  }
}

public static class SettingDefinition
{
  public static SettingDefinition<bool> CreateBoolean(string name, bool defaultValue)
  {
    return new(
      name,
      defaultValue,
      value => bool.TryParse(value, out var parsedValue)
        ? ParseResult<bool>.Success(parsedValue)
        : ParseResult<bool>.Failure(defaultValue),
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid boolean value.");
  }

  public static SettingDefinition<double> CreateDouble(string name, double defaultValue, double minimum)
  {
    return new(
      name,
      defaultValue,
      value => double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedValue)
        ? ParseResult<double>.Success(parsedValue)
        : ParseResult<double>.Failure(defaultValue),
      validate: value => value < minimum ? $"{name} must be greater than or equal to {minimum}." : null,
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid number value.");
  }

  public static SettingDefinition<TEnum> CreateEnum<TEnum>(string name, TEnum defaultValue)
    where TEnum : struct, Enum
  {
    return new(
      name,
      defaultValue,
      value => Enum.TryParse<TEnum>(value, true, out var parsedValue) && Enum.IsDefined(parsedValue)
        ? ParseResult<TEnum>.Success(parsedValue)
        : ParseResult<TEnum>.Failure(defaultValue),
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid {typeof(TEnum).Name} value.");
  }

  public static SettingDefinition<int> CreateInt(string name, int defaultValue, int minimum, int maximum)
  {
    return new(
      name,
      defaultValue,
      value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
        ? ParseResult<int>.Success(parsedValue)
        : ParseResult<int>.Failure(defaultValue),
      validate: value => value < minimum || value > maximum
        ? $"{name} must be between {minimum} and {maximum}."
        : null,
      invalidValueMessageFactory: settingName => $"{settingName} must be a valid integer value.");
  }
}
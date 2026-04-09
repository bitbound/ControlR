namespace ControlR.Libraries.Api.Contracts.Settings;

public sealed record SettingValueNormalizationResult(bool IsSuccess, string? Value, string? ErrorMessage)
{
  public static SettingValueNormalizationResult Failure(string errorMessage)
  {
    return new(false, null, errorMessage);
  }

  public static SettingValueNormalizationResult Success(string? value)
  {
    return new(true, value, null);
  }
}
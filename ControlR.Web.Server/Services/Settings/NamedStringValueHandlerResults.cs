
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Settings;

internal static class NamedStringValueHandlerResults
{
  public static HttpResult<string> NormalizeBoolean(string value, string settingName)
  {
    if (!bool.TryParse(value, out var parsedValue))
    {
      return HttpResult.Fail<string>(
        HttpResultErrorCode.ValidationFailed,
        $"{settingName} must be a valid boolean value.");
    }

    return HttpResult.Ok(parsedValue.ToString());
  }

  public static HttpResult<string> NormalizeEnum<TEnum>(string value, string settingName)
    where TEnum : struct, Enum
  {
    if (!Enum.TryParse<TEnum>(value, true, out var parsedValue) || !Enum.IsDefined(parsedValue))
    {
      return HttpResult.Fail<string>(
        HttpResultErrorCode.ValidationFailed,
        $"{settingName} must be a valid {typeof(TEnum).Name} value.");
    }

    return HttpResult.Ok(parsedValue.ToString());
  }
}
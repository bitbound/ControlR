using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Shared.DataValidation;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Settings;

internal sealed class AppendInstanceIdTenantSettingValueHandler : ITenantSettingValueHandler
{
  public string Name => TenantSettingNames.AppendInstanceId;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeBoolean(value, nameof(TenantSettingNames.AppendInstanceId));
  }
}

internal sealed class NotifyUserOnSessionStartTenantSettingValueHandler : ITenantSettingValueHandler
{
  public string Name => TenantSettingNames.NotifyUserOnSessionStart;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeBoolean(value, nameof(TenantSettingNames.NotifyUserOnSessionStart));
  }
}
internal sealed class InstanceIdTenantSettingValueHandler : ITenantSettingValueHandler
{
  public string Name => TenantSettingNames.InstanceId;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return HttpResult.Fail<string>(
        HttpResultErrorCode.ValidationFailed,
        $"{nameof(TenantSettingNames.InstanceId)} cannot be empty.");
    }

    var normalizedValue = value.Trim();
    var validationError = Validators.ValidateInstanceId(normalizedValue);
    if (validationError is null)
    {
      return HttpResult.Ok(normalizedValue);
    }

    return HttpResult.Fail<string>(
      HttpResultErrorCode.ValidationFailed,
      validationError);
  }
}
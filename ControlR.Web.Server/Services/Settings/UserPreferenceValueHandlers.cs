using ControlR.Libraries.Shared.DataValidation;
using ControlR.Libraries.Viewer.Common.Enums;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Client.Models;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.Settings;

internal sealed class HideOfflineDevicesUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.HideOfflineDevices;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeBoolean(value, nameof(UserPreferenceNames.HideOfflineDevices));
  }
}

internal sealed class KeyboardInputModeUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.KeyboardInputMode;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeEnum<KeyboardInputMode>(value, nameof(UserPreferenceNames.KeyboardInputMode));
  }
}

internal sealed class NotifyUserOnSessionStartUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.NotifyUserOnSessionStart;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeBoolean(value, nameof(UserPreferenceNames.NotifyUserOnSessionStart));
  }
}

internal sealed class OpenDeviceInNewTabUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.OpenDeviceInNewTab;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeBoolean(value, nameof(UserPreferenceNames.OpenDeviceInNewTab));
  }
}

internal sealed class ThemeModeUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.ThemeMode;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeEnum<ThemeMode>(value, nameof(UserPreferenceNames.ThemeMode));
  }
}

internal sealed class UserDisplayNameUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.UserDisplayName;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    var normalizedValue = value.Trim();

    if (normalizedValue.Length > 25)
    {
      return HttpResult.Fail<string>(
        HttpResultErrorCode.ValidationFailed,
        "User display name must be 25 characters or less.");
    }

    if (Validators.ValidateDisplayName(normalizedValue, out var illegalCharacters))
    {
      return HttpResult.Ok<string>(normalizedValue);
    }

    return HttpResult.Fail<string>(
      HttpResultErrorCode.ValidationFailed,
      $"User display name can only contain letters, numbers, underscores, hyphens, and spaces. Invalid characters: {string.Join(", ", illegalCharacters)}");
  }
}

internal sealed class ViewModeUserPreferenceValueHandler : IUserPreferenceValueHandler
{
  public string Name => UserPreferenceNames.ViewMode;

  public HttpResult<string> ValidateAndNormalize(string value)
  {
    return NamedStringValueHandlerResults.NormalizeEnum<ViewMode>(value, nameof(UserPreferenceNames.ViewMode));
  }
}
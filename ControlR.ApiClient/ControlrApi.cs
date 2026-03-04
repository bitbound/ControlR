using System.Net;
using ControlR.Libraries.Api.Contracts.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClient;

public interface IControlrApi
{
  IAgentUpdateApi AgentUpdate { get; }
  IAgentVersionApi AgentVersion { get; }
  IAuthApi Auth { get; }
  IDesktopPreviewApi DesktopPreview { get; }
  IDeviceFileSystemApi DeviceFileSystem { get; }
  IDevicesApi Devices { get; }
  IDeviceTagsApi DeviceTags { get; }
  IInstallerKeysApi InstallerKeys { get; }
  IInvitesApi Invites { get; }
  ILogonTokensApi LogonTokens { get; }
  IPersonalAccessTokensApi PersonalAccessTokens { get; }
  IPublicRegistrationSettingsApi PublicRegistrationSettings { get; }
  IRolesApi Roles { get; }
  IServerAlertApi ServerAlert { get; }
  IServerLogsApi ServerLogs { get; }
  IServerStatsApi ServerStats { get; }
  IServerVersionApi ServerVersion { get; }
  ITagsApi Tags { get; }
  ITenantSettingsApi TenantSettings { get; }
  ITestEmailApi TestEmail { get; }
  IUserPreferencesApi UserPreferences { get; }
  IUserRolesApi UserRoles { get; }
  IUsersApi Users { get; }
  IUserServerSettingsApi UserServerSettings { get; }
  IUserTagsApi UserTags { get; }
}

public partial class ControlrApi(
  HttpClient httpClient,
  ILogger<ControlrApi> logger,
  IOptions<ControlrApiClientOptions> options) :
  IControlrApi,
  IAgentUpdateApi,
  IAuthApi,
  IDesktopPreviewApi,
  IDeviceFileSystemApi,
  IDeviceTagsApi,
  IDevicesApi,
  IInstallerKeysApi,
  IInvitesApi,
  ILogonTokensApi,
  IPersonalAccessTokensApi,
  IPublicRegistrationSettingsApi,
  IRolesApi,
  IServerAlertApi,
  IServerLogsApi,
  IServerStatsApi,
  ITagsApi,
  ITenantSettingsApi,
  ITestEmailApi,
  IUserPreferencesApi,
  IUserRolesApi,
  IUserServerSettingsApi,
  IUserTagsApi,
  IUsersApi,
  IAgentVersionApi,
  IServerVersionApi
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;
  private readonly IOptions<ControlrApiClientOptions> _options = options;

  public IAgentUpdateApi AgentUpdate => this;
  public IAgentVersionApi AgentVersion => this;
  public IAuthApi Auth => this;
  public IDesktopPreviewApi DesktopPreview => this;
  public IDeviceFileSystemApi DeviceFileSystem => this;
  public IDevicesApi Devices => this;
  public IDeviceTagsApi DeviceTags => this;
  public IInstallerKeysApi InstallerKeys => this;
  public IInvitesApi Invites => this;
  public ILogonTokensApi LogonTokens => this;
  public IPersonalAccessTokensApi PersonalAccessTokens => this;
  public IPublicRegistrationSettingsApi PublicRegistrationSettings => this;
  public IRolesApi Roles => this;
  public IServerAlertApi ServerAlert => this;
  public IServerLogsApi ServerLogs => this;
  public IServerStatsApi ServerStats => this;
  public IServerVersionApi ServerVersion => this;
  public ITagsApi Tags => this;
  public ITenantSettingsApi TenantSettings => this;
  public ITestEmailApi TestEmail => this;
  public IUserPreferencesApi UserPreferences => this;
  public IUserRolesApi UserRoles => this;
  public IUsersApi Users => this;
  public IUserServerSettingsApi UserServerSettings => this;
  public IUserTagsApi UserTags => this;

  private async Task<ApiResult> ExecuteApiCall(Func<Task> func)
  {
    try
    {
      await func.Invoke();
      return ApiResult.Ok();
    }
    catch (HttpRequestException ex)
    {
      var apiResult = ApiResult.Fail(ex.Message, ex.StatusCode, ex.HttpRequestError);
      return LogFailure(apiResult, ex);
    }
    catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
    {
      const string message = "The operation was canceled by the caller.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (TaskCanceledException ex)
    {
      const string message = "The request timed out.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (Exception ex)
    {
      const string message = "The request to the server failed.";
      var apiResult = ApiResult.Fail(message, HttpStatusCode.InternalServerError);
      return LogFailure(apiResult, ex);
    }
  }

  private async Task<ApiResult<T>> ExecuteApiCall<T>(Func<Task<T?>> func)
  {
    try
    {
      var resultValue = await func.Invoke() ??
        throw new HttpRequestException("The server response was empty.");

      var validationErrors = DtoValidatorFactory.Validate(resultValue);
      if (validationErrors is not null)
      {
        if (_options.Value.DisableResponseDtoStrictness)
        {
          _logger.LogWarning("Response DTO validation failed but strictness is disabled: {Reason}", validationErrors);
          return ApiResult.Ok(resultValue);
        }

        var reason = $"DTO validation failed: {validationErrors}";
        var apiResult = ApiResult.Fail<T>(reason, HttpStatusCode.InternalServerError);
        return LogFailure(apiResult);
      }

      return ApiResult.Ok(resultValue);
    }
    catch (HttpRequestException ex)
    {
      var apiResult = ApiResult.Fail<T>(ex.Message, ex.StatusCode, ex.HttpRequestError);
      return LogFailure(apiResult, ex);
    }
    catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
    {
      const string message = "The operation was canceled by the caller.";
      var apiResult = ApiResult.Fail<T>(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (TaskCanceledException ex)
    {
      const string message = "The request timed out.";
      var apiResult = ApiResult.Fail<T>(message, HttpStatusCode.RequestTimeout);
      return LogFailure(apiResult, ex);
    }
    catch (Exception ex)
    {
      const string message = "The request to the server failed.";
      var apiResult = ApiResult.Fail<T>(message, httpRequestError: HttpRequestError.Unknown);
      return LogFailure(apiResult, ex);
    }
  }

  private ApiResult LogFailure(ApiResult result, Exception? ex = null)
  {
    if (ex is null)
    {
      _logger.LogWarning("API request failed: {Reason}", result.Reason);
      return result;
    }

    _logger.LogError(ex, "API request failed: {Reason}", result.Reason);
    return result;
  }

  private ApiResult<T> LogFailure<T>(ApiResult<T> result, Exception? ex = null)
  {
    if (ex is null)
    {
      _logger.LogWarning("API request failed: {Reason}", result.Reason);
      return result;
    }

    _logger.LogError(ex, "API request failed: {Reason}", result.Reason);
    return result;
  }
}
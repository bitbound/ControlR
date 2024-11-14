using System.Net.Http.Json;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IControlrApi
{
  Task<Result> AddDeviceTag(Guid deviceId, Guid tagId);
  Task<Result> AddUserRole(Guid userId, Guid roleId);
  Task<Result> AddUserTag(Guid userId, Guid tagId);
  Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType);
  Task<Result> DeleteDevice(Guid deviceId);
  Task<Result> DeleteTag(Guid tagId);
  IAsyncEnumerable<DeviceDto> GetAllDevices();
  Task<Result<TagResponseDto[]>> GetAllowedTags();
  Task<Result<RoleResponseDto[]>> GetAllRoles();
  Task<Result<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false);
  Task<Result<UserResponseDto[]>> GetAllUsers();
  Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
  Task<Result<Version>> GetCurrentAgentVersion();
  Task<Result<Version>> GetCurrentServerVersion();
  Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime);
  Task<Result<ServerSettingsDto>> GetServerSettings();
  Task<Result<UserPreferenceResponseDto>> GetUserPreference(string preferenceName);
  Task<Result<TagResponseDto[]>> GetUserTags(Guid userId, bool includeLinkedIds = false);
  Task<Result> RemoveDeviceTag(Guid deviceId, Guid tagId);
  Task<Result> RemoveUserRole(Guid userId, Guid roleId);
  Task<Result> RemoveUserTag(Guid userId, Guid tagId);
  Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue);
}

public class ControlrApi(
  HttpClient httpClient,
  ILogger<ControlrApi> logger) : IControlrApi
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;

  public async Task<Result> AddDeviceTag(Guid deviceId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var dto = new DeviceTagAddRequestDto(deviceId, tagId);
      var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceTagsEndpoint}", dto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> AddUserRole(Guid userId, Guid roleId)
  {
    return await TryCallApi(async () =>
    {
      var dto = new UserRoleAddRequestDto(userId, roleId);
      var response = await _client.PostAsJsonAsync($"{HttpConstants.UserRolesEndpoint}", dto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> AddUserTag(Guid userId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var dto = new UserTagAddRequestDto(userId, tagId);
      var response = await _client.PostAsJsonAsync($"{HttpConstants.UserTagsEndpoint}", dto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType)
  {
    return await TryCallApi(async () =>
    {
      var request = new TagCreateRequestDto(tagName, tagType);
      var response = await _client.PostAsJsonAsync(HttpConstants.TagsEndpoint, request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>();
    });
  }

  public async Task<Result> DeleteDevice(Guid deviceId)
  {
    return await TryCallApi(async () =>
    {
      var response = await _client.DeleteAsync($"{HttpConstants.DevicesEndpoint}/{deviceId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> DeleteTag(Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var response = await _client.DeleteAsync($"{HttpConstants.TagsEndpoint}/{tagId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async IAsyncEnumerable<DeviceDto> GetAllDevices()
  {
    var stream = _client.GetFromJsonAsAsyncEnumerable<DeviceDto>(HttpConstants.DevicesEndpoint);
    await foreach (var device in stream)
    {
      if (device is null)
      {
        continue;
      }

      yield return device;
    }
  }

  public async Task<Result<TagResponseDto[]>> GetAllowedTags()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<TagResponseDto[]>(HttpConstants.UserTagsEndpoint));
  }

  public async Task<Result<RoleResponseDto[]>> GetAllRoles()
  {
    return await TryCallApi(async () => 
      await _client.GetFromJsonAsync<RoleResponseDto[]>(HttpConstants.RolesEndpoint));
  }

  public async Task<Result<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false)
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<TagResponseDto[]>(
        $"{HttpConstants.TagsEndpoint}?includeLinkedIds={includeLinkedIds}"));
  }

  public async Task<Result<UserResponseDto[]>> GetAllUsers()
  {
    return await TryCallApi(async () => 
      await _client.GetFromJsonAsync<UserResponseDto[]>(HttpConstants.UsersEndpoint));
  }

  public async Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime)
  {
    try
    {
      var fileRelativePath = AppConstants.GetAgentFileDownloadPath(runtime);
      using var request = new HttpRequestMessage(HttpMethod.Head, fileRelativePath);
      using var response = await _client.SendAsync(request);
      response.EnsureSuccessStatusCode();
      if (!response.Headers.TryGetValues("Content-Hash", out var values) ||
          values.FirstOrDefault() is not { } hashString)
      {
        return Result.Fail<byte[]>("Failed to get agent file hash.");
      }

      var fileHash = Convert.FromHexString(hashString);
      return Result.Ok(fileHash);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking for new agent hash.");
      return Result.Fail<byte[]>(ex);
    }
  }

  public async Task<Result<Version>> GetCurrentAgentVersion()
  {
    return await TryCallApi(async () =>
    {
      var version = await _client.GetFromJsonAsync<Version>(HttpConstants.AgentVersionEndpoint);
      _logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", version);
      return version;
    });
  }

  public async Task<Result<Version>> GetCurrentServerVersion()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<Version>(HttpConstants.ServerVersionEndpoint));
  }

  public async Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime)
  {
    try
    {
      var fileRelativePath = AppConstants.GetStreamerFileDownloadPath(runtime);
      using var request = new HttpRequestMessage(HttpMethod.Head, fileRelativePath);
      using var response = await _client.SendAsync(request);
      response.EnsureSuccessStatusCode();

      if (!response.Headers.TryGetValues("Content-Hash", out var values) ||
          values.FirstOrDefault() is not { } hashString)
      {
        return Result.Fail<byte[]>("Failed to get streamer file hash.");
      }

      var fileHash = Convert.FromHexString(hashString);
      return Result.Ok(fileHash);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking for new streamer hash.");
      return Result.Fail<byte[]>(ex);
    }
  }

  public async Task<Result<ServerSettingsDto>> GetServerSettings()
  {
    return await TryCallApi(async () => 
      await _client.GetFromJsonAsync<ServerSettingsDto>(HttpConstants.ServerSettingsEndpoint));
  }

  public async Task<Result<UserPreferenceResponseDto>> GetUserPreference(string preferenceName)
  {
    return await TryCallApi(async () => 
      await _client.GetFromJsonAsync<UserPreferenceResponseDto>(
        $"{HttpConstants.UserPreferencesEndpoint}/{preferenceName}"));
  }

  public async Task<Result<TagResponseDto[]>> GetUserTags(Guid userId, bool includeLinkedIds = false)
  {
    return await TryCallApi(async () => 
      await _client.GetFromJsonAsync<TagResponseDto[]>(
        $"{HttpConstants.UserTagsEndpoint}/{userId}"));
  }

  public async Task<Result> RemoveDeviceTag(Guid deviceId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var response = await _client.DeleteAsync($"{HttpConstants.DeviceTagsEndpoint}/{deviceId}/{tagId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> RemoveUserRole(Guid userId, Guid roleId)
  {
    return await TryCallApi(async () =>
    {
      var response = await _client.DeleteAsync($"{HttpConstants.UserRolesEndpoint}/{userId}/{roleId}");
      response.EnsureSuccessStatusCode();
    });
  }


  public async Task<Result> RemoveUserTag(Guid userId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var response = await _client.DeleteAsync($"{HttpConstants.UserTagsEndpoint}/{userId}/{tagId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue)
  {
    return await TryCallApi(async () =>
    {
      var request = new UserPreferenceRequestDto(preferenceName, preferenceValue);
      var response = await _client.PostAsJsonAsync(HttpConstants.UserPreferencesEndpoint, request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>();
    });
  }

  private async Task<Result> TryCallApi(Func<Task> func)
  {
    try
    {
      await func.Invoke();
      return Result.Ok();
    }
    catch (HttpRequestException ex)
    {
      return Result
        .Fail(ex, ex.Message)
        .Log(_logger);
    }
    catch (Exception ex)
    {
      return Result
        .Fail(ex, "The request to the server failed.")
        .Log(_logger);
    }
  }

  private async Task<Result<T>> TryCallApi<T>(Func<Task<T?>> func)
  {
    try
    {
      var resultValue = await func.Invoke() ??
        throw new HttpRequestException("The server response was empty.");

      return Result.Ok(resultValue);
    }
    catch (HttpRequestException ex)
    {
      return Result
        .Fail<T>(ex, ex.Message)
        .Log(_logger);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<T>(ex, "The request to the server failed.")
        .Log(_logger);
    }
  }
}
using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IControlrApi
{
  Task<Result<List<DeviceGroupDto>>> GetAllDeviceGroups();
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices();

  Task<Result<List<TagResponseDto>>> GetAllTags(bool includeLinkedIds = false);
  Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType);
  Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
  Task<Result<Version>> GetCurrentAgentVersion();
  Task<Result<Version>> GetCurrentServerVersion();
  Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime);
  Task<Result<ServerSettingsDto>> GetServerSettings();
  Task<Result<UserPreferenceResponseDto>> GetUserPreference(string preferenceName);
  Task<Result<List<TagResponseDto>>> GetUserTags(Guid userId, bool includeLinkedIds = false);
  Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue);
  Task<Result> DeleteTag(Guid tagId);
}

public class ControlrApi(
  HttpClient httpClient,
  ILogger<ControlrApi> logger) : IControlrApi
{
  private const string AgentVersionEndpoint = "/api/version/agent";
  private const string DeviceGroupsEndpoint = "/api/device-groups";
  private const string DevicesEndpoint = "/api/devices";
  private const string ServerSettingsEndpoint = "/api/server-settings";
  private const string ServerVersionEndpoint = "/api/version/server";
  private const string TagsEndpoint = "/api/tags";
  private const string UserPreferencesEndpoint = "/api/user-preferences";
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;

  public async Task<Result<List<DeviceGroupDto>>> GetAllDeviceGroups()
  {
    try
    {
      var deviceGroups = await _client.GetFromJsonAsync<List<DeviceGroupDto>>(DeviceGroupsEndpoint);
      if (deviceGroups is null)
      {
        return Result.Fail<List<DeviceGroupDto>>("Server response was empty.");
      }

      return Result.Ok(deviceGroups);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<List<DeviceGroupDto>>(ex, "Error while getting device groups.")
        .Log(_logger);
    }
  }

  public async IAsyncEnumerable<DeviceResponseDto> GetAllDevices()
  {
    var stream = _client.GetFromJsonAsAsyncEnumerable<DeviceResponseDto>(DevicesEndpoint);
    await foreach (var device in stream)
    {
      if (device is null)
      {
        continue;
      }

      yield return device;
    }
  }

  public async Task<Result<List<TagResponseDto>>> GetAllTags(bool includeLinkedIds = false)
  {
    try
    {
      var tags = await _client.GetFromJsonAsync<List<TagResponseDto>>(
        $"{TagsEndpoint}?includeLinkedIds={includeLinkedIds}");
      if (tags is null)
      {
        return Result.Fail<List<TagResponseDto>>("Server response was empty.");
      }

      return Result.Ok(tags);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<List<TagResponseDto>>(ex, "Error while getting user tags.")
        .Log(_logger);
    }
  }

  public async Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType)
  {
    try
    {
      var request = new TagCreateRequestDto(tagName, tagType);
      var response = await _client.PostAsJsonAsync(TagsEndpoint, request);
      response.EnsureSuccessStatusCode();
      var dto = await response.Content.ReadFromJsonAsync<TagResponseDto>();
      if (dto is null)
      {
        return Result.Fail<TagResponseDto>("Server response was empty.");
      }

      return Result.Ok(dto);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<TagResponseDto>(ex, "Error while creating tag.")
        .Log(_logger);
    }
  }

  public async Task<Result> DeleteTag(Guid tagId)
  {
    try
    {
      var response = await _client.DeleteAsync($"{TagsEndpoint}/{tagId}");
      response.EnsureSuccessStatusCode();
      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result
        .Fail(ex, "Error while deleting tag.")
        .Log(_logger);
    }
  }

  public async Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime)
  {
    try
    {
      var fileRelativePath = AppConstants.GetAgentFileDownloadPath(runtime);
      using var request = new HttpRequestMessage(HttpMethod.Head, fileRelativePath);
      using var response = await _client.SendAsync(request);
      response.EnsureSuccessStatusCode();
      if (response.Headers.TryGetValues("Content-Hash", out var values) &&
          values.FirstOrDefault() is { } hashString)
      {
        var fileHash = Convert.FromHexString(hashString);
        return Result.Ok(fileHash);
      }

      return Result.Fail<byte[]>("Failed to get agent file hash.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking for new agent hash.");
      return Result.Fail<byte[]>(ex);
    }
  }

  public async Task<Result<Version>> GetCurrentAgentVersion()
  {
    try
    {
      var version = await _client.GetFromJsonAsync<Version>(AgentVersionEndpoint);
      _logger.LogInformation("Latest Agent version on server: {LatestAgentVersion}", version);
      if (version is null)
      {
        return Result.Fail<Version>("Server response was empty.");
      }

      return Result.Ok(version);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<Version>(ex, "Error while checking for new agent version.")
        .Log(_logger);
    }
  }

  public async Task<Result<Version>> GetCurrentServerVersion()
  {
    try
    {
      var serverVersion = await _client.GetFromJsonAsync<Version>(ServerVersionEndpoint);
      if (serverVersion is null)
      {
        return Result.Fail<Version>("Server response was empty.");
      }

      return Result.Ok(serverVersion);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<Version>(ex, "Error while getting server version.")
        .Log(_logger);
    }
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
    try
    {
      var serverSettings = await _client.GetFromJsonAsync<ServerSettingsDto>(ServerSettingsEndpoint);
      if (serverSettings is null)
      {
        return Result.Fail<ServerSettingsDto>("Server settings response was empty.");
      }

      return Result.Ok(serverSettings);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<ServerSettingsDto>(ex, "Error while getting server settings.")
        .Log(_logger);
    }
  }

  public async Task<Result<UserPreferenceResponseDto>> GetUserPreference(string preferenceName)
  {
    try
    {
      var response =
        await _client.GetFromJsonAsync<UserPreferenceResponseDto>($"{UserPreferencesEndpoint}/{preferenceName}");
      if (response is null)
      {
        return Result.Fail<UserPreferenceResponseDto>("User preference not found.");
      }

      return Result.Ok(response);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      return Result.Fail<UserPreferenceResponseDto>(ex);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<UserPreferenceResponseDto>(ex, "Error while getting user preference.")
        .Log(_logger);
    }
  }

  public async Task<Result<List<TagResponseDto>>> GetUserTags(Guid userId, bool includeLinkedIds = false)
  {
    try
    {
      var tags = await _client.GetFromJsonAsync<List<TagResponseDto>>(
        $"{TagsEndpoint}/{userId}?includeLinkedIds={includeLinkedIds}");
      if (tags is null)
      {
        return Result.Fail<List<TagResponseDto>>("Server response was empty.");
      }

      return Result.Ok(tags);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<List<TagResponseDto>>(ex, "Error while getting user tags.")
        .Log(_logger);
    }
  }

  public async Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue)
  {
    try
    {
      var request = new UserPreferenceRequestDto(preferenceName, preferenceValue);
      var response = await _client.PostAsJsonAsync(UserPreferencesEndpoint, request);
      response.EnsureSuccessStatusCode();
      var dto = await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>();
      if (dto is null)
      {
        return Result.Fail<UserPreferenceResponseDto>("Server response was empty.");
      }

      return Result.Ok(dto);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<UserPreferenceResponseDto>(ex, "Error while setting user preference.")
        .Log(_logger);
    }
  }
}
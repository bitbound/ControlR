using System.Net;
using System.Net.Http.Json;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IControlrApi
{
  Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType);
  Task<Result> DeleteDevice(Guid deviceId);
  Task<Result> DeleteTag(Guid tagId);
  IAsyncEnumerable<DeviceUpdateResponseDto> GetAllDevices();

  Task<Result<IReadOnlyList<TagResponseDto>>> GetAllTags(bool includeLinkedIds = false);
  Task<Result<IReadOnlyList<UserResponseDto>>> GetAllUsers();
  Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
  Task<Result<Version>> GetCurrentAgentVersion();
  Task<Result<Version>> GetCurrentServerVersion();
  Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime);
  Task<Result<ServerSettingsDto>> GetServerSettings();
  Task<Result<UserPreferenceResponseDto>> GetUserPreference(string preferenceName);
  Task<Result<IReadOnlyList<TagResponseDto>>> GetUserTags(Guid userId, bool includeLinkedIds = false);
  Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue);
}

public class ControlrApi(
  HttpClient httpClient,
  ILogger<ControlrApi> logger) : IControlrApi
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;

  public async Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType)
  {
    try
    {
      var request = new TagCreateRequestDto(tagName, tagType);
      var response = await _client.PostAsJsonAsync(HttpConstants.TagsEndpoint, request);
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

  public async Task<Result> DeleteDevice(Guid deviceId)
  {
    try
    {
      var response = await _client.DeleteAsync($"{HttpConstants.DevicesEndpoint}/{deviceId}");
      response.EnsureSuccessStatusCode();
      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result
        .Fail(ex, "Error while deleting device.")
        .Log(_logger);
    }
  }

  public async Task<Result> DeleteTag(Guid tagId)
  {
    try
    {
      var response = await _client.DeleteAsync($"{HttpConstants.TagsEndpoint}/{tagId}");
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

  public async IAsyncEnumerable<DeviceUpdateResponseDto> GetAllDevices()
  {
    var stream = _client.GetFromJsonAsAsyncEnumerable<DeviceUpdateResponseDto>(HttpConstants.DevicesEndpoint);
    await foreach (var device in stream)
    {
      if (device is null)
      {
        continue;
      }

      yield return device;
    }
  }

  public async Task<Result<IReadOnlyList<TagResponseDto>>> GetAllTags(bool includeLinkedIds = false)
  {
    try
    {
      var tags = await _client.GetFromJsonAsync<IReadOnlyList<TagResponseDto>>(
        $"{HttpConstants.TagsEndpoint}?includeLinkedIds={includeLinkedIds}");

      return tags is null
        ? Result.Fail<IReadOnlyList<TagResponseDto>>("Server response was empty.")
        : Result.Ok(tags);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<IReadOnlyList<TagResponseDto>>(ex, "Error while getting user tags.")
        .Log(_logger);
    }
  }

  public async Task<Result<IReadOnlyList<UserResponseDto>>> GetAllUsers()
  {
    try
    {
      var users = await _client.GetFromJsonAsync<IReadOnlyList<UserResponseDto>>(HttpConstants.UsersEndpoint);
      return users is null
        ? Result.Fail<IReadOnlyList<UserResponseDto>>("Server response was empty.")
        : Result.Ok(users);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<IReadOnlyList<UserResponseDto>>(ex, "Error while getting users.")
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
    try
    {
      var version = await _client.GetFromJsonAsync<Version>(HttpConstants.AgentVersionEndpoint);
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
      var serverVersion = await _client.GetFromJsonAsync<Version>(HttpConstants.ServerVersionEndpoint);
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
      var serverSettings = await _client.GetFromJsonAsync<ServerSettingsDto>(HttpConstants.ServerSettingsEndpoint);
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
        await _client.GetFromJsonAsync<UserPreferenceResponseDto>($"{HttpConstants.UserPreferencesEndpoint}/{preferenceName}");
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

  public async Task<Result<IReadOnlyList<TagResponseDto>>> GetUserTags(Guid userId, bool includeLinkedIds = false)
  {
    try
    {
      var tags = await _client.GetFromJsonAsync<IReadOnlyList<TagResponseDto>>(
        $"{HttpConstants.TagsEndpoint}/{userId}?includeLinkedIds={includeLinkedIds}");
      if (tags is null)
      {
        return Result.Fail<IReadOnlyList<TagResponseDto>>("Server response was empty.");
      }

      return Result.Ok(tags);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<IReadOnlyList<TagResponseDto>>(ex, "Error while getting user tags.")
        .Log(_logger);
    }
  }

  public async Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue)
  {
    try
    {
      var request = new UserPreferenceRequestDto(preferenceName, preferenceValue);
      var response = await _client.PostAsJsonAsync(HttpConstants.UserPreferencesEndpoint, request);
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
using ControlR.Libraries.Shared.Dtos.ServerApi;
using System.Net.Http.Json;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IControlrApi
{
  Task<Result<List<DeviceGroupDto>>> GetAllDeviceGroups();
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices();

  Task<Result<List<TagDto>>> GetAllTags(bool includeLinkedIds = false);

  Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
  Task<Result<Version>> GetCurrentAgentVersion();
  Task<Result<Version>> GetCurrentServerVersion();
  Task<Result<byte[]>> GetCurrentStreamerHash(RuntimeId runtime);
  Task<Result<ServerSettingsDto>> GetServerSettings();
  Task<Result<UserPreferenceResponseDto>> GetUserPreference(string preferenceName);
  Task<Result<List<TagDto>>> GetUserTags(Guid userId, bool includeLinkedIds = false);
  Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue);
}

public class ControlrApi(
  HttpClient httpClient,
  ILogger<ControlrApi> logger) : IControlrApi
{
  private const string _agentVersionEndpoint = "/api/version/agent";
  private const string _deviceGroupsEndpoint = "/api/device-groups";
  private const string _devicesEndpoint = "/api/devices";
  private const string _serverSettingsEndpoint = "/api/server-settings";
  private const string _serverVersionEndpoint = "/api/version/server";
  private const string _tagsEndpoint = "/api/tags";
  private const string _userPreferencesEndpoint = "/api/user-preferences";
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;

  public async Task<Result<List<DeviceGroupDto>>> GetAllDeviceGroups()
  {
    try
    {
      var deviceGroups = await _client.GetFromJsonAsync<List<DeviceGroupDto>>(_deviceGroupsEndpoint);
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
    var stream = _client.GetFromJsonAsAsyncEnumerable<DeviceResponseDto>(_devicesEndpoint);
    await foreach (var device in stream)
    {
      if (device is null)
      {
        continue;
      }
      yield return device;
    }
  }

  public async Task<Result<List<TagDto>>> GetAllTags(bool includeLinkedIds = false)
  {
    try
    {
      var tags = await _client.GetFromJsonAsync<List<TagDto>>($"{_tagsEndpoint}?includeLinkedIds={includeLinkedIds}");
      if (tags is null)
      {
        return Result.Fail<List<TagDto>>("Server response was empty.");
      }
      return Result.Ok(tags);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<List<TagDto>>(ex, "Error while getting user tags.")
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
          values.FirstOrDefault() is string hashString)
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
      var version = await _client.GetFromJsonAsync<Version>(_agentVersionEndpoint);
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
      var serverVersion = await _client.GetFromJsonAsync<Version>(_serverVersionEndpoint);
      if (serverVersion is null)
      {
        return Result.Fail<Version>("Server response was empty.");
      }
      return Result.Ok<Version>(serverVersion);
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

      if (response.Headers.TryGetValues("Content-Hash", out var values) &&
            values.FirstOrDefault() is string hashString)
      {
        var fileHash = Convert.FromHexString(hashString);
        return Result.Ok(fileHash);
      }

      return Result.Fail<byte[]>("Failed to get streamer file hash.");
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
      var serverSettings = await _client.GetFromJsonAsync<ServerSettingsDto>(_serverSettingsEndpoint);
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
      var response = await _client.GetFromJsonAsync<UserPreferenceResponseDto>($"{_userPreferencesEndpoint}/{preferenceName}");
      if (response is null)
      {
        return Result.Fail<UserPreferenceResponseDto>("User preference not found.");
      }

      return Result.Ok(response);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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

  public async Task<Result<List<TagDto>>> GetUserTags(Guid userId, bool includeLinkedIds = false)
  {
    try
    {
      var tags = await _client.GetFromJsonAsync<List<TagDto>>($"{_tagsEndpoint}/{userId}?includeLinkedIds={includeLinkedIds}");
      if (tags is null)
      {
        return Result.Fail<List<TagDto>>("Server response was empty.");
      }
      return Result.Ok(tags);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<List<TagDto>>(ex, "Error while getting user tags.")
        .Log(_logger);
    }
  }
  public async Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue)
  {
    try
    {
      var request = new UserPreferenceRequestDto(preferenceName, preferenceValue);
      var response = await _client.PostAsJsonAsync(_userPreferencesEndpoint, request);
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
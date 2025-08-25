using System.Net.Http.Json;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Services.Http;

public interface IControlrApi
{
  Task<Result<AcceptInvitationResponseDto>> AcceptInvitation(
      string activationCode,
      string emailAddress,
      string password);

  Task<Result> AddDeviceTag(Guid deviceId, Guid tagId);
  Task<Result> AddUserRole(Guid userId, Guid roleId);
  Task<Result> AddUserTag(Guid userId, Guid tagId);
  Task<Result<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request);
  Task<Result> CreateDevice(DeviceDto device, string installerKey);
  Task<Result<CreateInstallerKeyResponseDto>> CreateInstallerKey(CreateInstallerKeyRequestDto dto);
  Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType);
  Task<Result<TenantInviteResponseDto>> CreateTenantInvite(string invteeEmail);

  Task<Result> DeleteDevice(Guid deviceId);
  Task<Result> DeletePersonalAccessToken(Guid personalAccessTokenId);
  Task<Result> DeleteTag(Guid tagId);
  Task<Result> DeleteTenantInvite(Guid inviteId);
  Task<Result> DeleteTenantSetting(string settingName);
  Task<Result> DeleteUser(Guid userId);
  IAsyncEnumerable<DeviceDto> GetAllDevices();
  Task<Result<PersonalAccessTokenDto[]>> GetPersonalAccessTokens();
  Task<Result<RoleResponseDto[]>> GetAllRoles();
  Task<Result<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false);
  Task<Result<UserResponseDto[]>> GetAllUsers();
  Task<Result<TagResponseDto[]>> GetAllowedTags();
  Task<Result<byte[]>> GetCurrentAgentHash(RuntimeId runtime);
  Task<Result<Version>> GetCurrentAgentVersion();
  Task<Result<byte[]>> GetCurrentDesktopClientHash(RuntimeId runtime);
  Task<Result<Version>> GetCurrentServerVersion();
  Task<Result<DeviceDto>> GetDevice(Guid deviceId);
  Task<Result<TenantInviteResponseDto[]>> GetPendingTenantInvites();
  Task<Result<PublicRegistrationSettings>> GetPublicRegistrationSettings();
  Task<Result<TenantSettingResponseDto?>> GetTenantSetting(string settingName);
  Task<Result<UserPreferenceResponseDto?>> GetUserPreference(string preferenceName);
  Task<Result<TagResponseDto[]>> GetUserTags(Guid userId, bool includeLinkedIds = false);
  Task<Result> LogOut();
  Task<Result> RemoveDeviceTag(Guid deviceId, Guid tagId);
  Task<Result> RemoveUserRole(Guid userId, Guid roleId);
  Task<Result> RemoveUserTag(Guid userId, Guid tagId);
  Task<Result<TagResponseDto>> RenameTag(Guid tagId, string newTagName);
  Task<Result<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request);
  Task<Result<TenantSettingResponseDto>> SetTenantSetting(string settingName, string settingValue);
  Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue);
  Task<Result<PersonalAccessTokenDto>> UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request);
}

public class ControlrApi(
  HttpClient httpClient,
  ILogger<ControlrApi> logger) : IControlrApi
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ControlrApi> _logger = logger;

  public async Task<Result<AcceptInvitationResponseDto>> AcceptInvitation(
    string activationCode,
    string emailAddress,
    string password)
  {
    return await TryCallApi(async () =>
    {
      var dto = new AcceptInvitationRequestDto(activationCode, emailAddress, password);
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.InvitesEndpoint}/accept", dto);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<AcceptInvitationResponseDto>();
    });
  }

  public async Task<Result> AddDeviceTag(Guid deviceId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var dto = new DeviceTagAddRequestDto(deviceId, tagId);
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DeviceTagsEndpoint}", dto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> AddUserRole(Guid userId, Guid roleId)
  {
    return await TryCallApi(async () =>
    {
      var dto = new UserRoleAddRequestDto(userId, roleId);
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UserRolesEndpoint}", dto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> AddUserTag(Guid userId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      var dto = new UserTagAddRequestDto(userId, tagId);
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.UserTagsEndpoint}", dto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> CreateDevice(DeviceDto device, string installerKey)
  {
    return await TryCallApi(async () =>
    {
      var requestDto = new CreateDeviceRequestDto(device, installerKey);
      using var response = await _client.PostAsJsonAsync(HttpConstants.DevicesEndpoint, requestDto);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.PersonalAccessTokensEndpoint, request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<CreatePersonalAccessTokenResponseDto>();
    });
  }

  public async Task<Result<CreateInstallerKeyResponseDto>> CreateInstallerKey(CreateInstallerKeyRequestDto dto)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.InstallerKeysEndpoint, dto);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<CreateInstallerKeyResponseDto>();
    });
  }

  public async Task<Result<TagResponseDto>> CreateTag(string tagName, TagType tagType)
  {
    return await TryCallApi(async () =>
    {
      var request = new TagCreateRequestDto(tagName, tagType);
      using var response = await _client.PostAsJsonAsync(HttpConstants.TagsEndpoint, request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>();
    });
  }

  public async Task<Result<TenantInviteResponseDto>> CreateTenantInvite(string invteeEmail)
  {
    return await TryCallApi(async () =>
    {
      var request = new TenantInviteRequestDto(invteeEmail);
      using var response = await _client.PostAsJsonAsync(HttpConstants.InvitesEndpoint, request);
      if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
      {
        var content = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(content))
        {
          return Result.Fail<TenantInviteResponseDto>(content);
        }
      }
      response.EnsureSuccessStatusCode();
      var inviteDto = await response.Content.ReadFromJsonAsync<TenantInviteResponseDto>();
      if (inviteDto is null)
      {
        return Result.Fail<TenantInviteResponseDto>("Server response was empty.");
      }

      return Result.Ok(inviteDto);
    });
  }

  public async Task<Result> DeleteDevice(Guid deviceId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.DevicesEndpoint}/{deviceId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> DeletePersonalAccessToken(Guid personalAccessTokenId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.PersonalAccessTokensEndpoint}/{personalAccessTokenId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> DeleteTag(Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.TagsEndpoint}/{tagId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> DeleteTenantInvite(Guid inviteId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.InvitesEndpoint}/{inviteId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> DeleteUser(Guid userId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UsersEndpoint}/{userId}");
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

  public async Task<Result<PersonalAccessTokenDto[]>> GetPersonalAccessTokens()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<PersonalAccessTokenDto[]>(HttpConstants.PersonalAccessTokensEndpoint));
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

  public async Task<Result<TagResponseDto[]>> GetAllowedTags()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<TagResponseDto[]>(HttpConstants.UserTagsEndpoint));
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

  public async Task<Result<byte[]>> GetCurrentDesktopClientHash(RuntimeId runtime)
  {
    try
    {
      var fileRelativePath = AppConstants.GetDesktopClientDownloadPath(runtime);
      using var request = new HttpRequestMessage(HttpMethod.Head, fileRelativePath);
      using var response = await _client.SendAsync(request);
      response.EnsureSuccessStatusCode();

      if (!response.Headers.TryGetValues("Content-Hash", out var values) ||
          values.FirstOrDefault() is not { } hashString)
      {
        return Result.Fail<byte[]>("Failed to get desktop client file hash.");
      }

      var fileHash = Convert.FromHexString(hashString);
      return Result.Ok(fileHash);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while checking for new desktop client hash.");
      return Result.Fail<byte[]>(ex);
    }
  }

  public async Task<Result<Version>> GetCurrentServerVersion()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<Version>(HttpConstants.ServerVersionEndpoint));
  }

  public async Task<Result<DeviceDto>> GetDevice(Guid deviceId)
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<DeviceDto>($"{HttpConstants.DevicesEndpoint}/{deviceId}"));
  }
  public async Task<Result<TenantInviteResponseDto[]>> GetPendingTenantInvites()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<TenantInviteResponseDto[]>(HttpConstants.InvitesEndpoint));
  }

  public async Task<Result<PublicRegistrationSettings>> GetPublicRegistrationSettings()
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<PublicRegistrationSettings>(HttpConstants.PublicRegistrationSettingsEndpoint));
  }

  public async Task<Result<UserPreferenceResponseDto?>> GetUserPreference(string preferenceName)
  {
    return await TryGetNullableResponse(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.UserPreferencesEndpoint}/{preferenceName}");
      if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
      {
        return null;
      }
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>();
    });
  }

  public async Task<Result<TagResponseDto[]>> GetUserTags(Guid userId, bool includeLinkedIds = false)
  {
    return await TryCallApi(async () =>
      await _client.GetFromJsonAsync<TagResponseDto[]>(
        $"{HttpConstants.UserTagsEndpoint}/{userId}"));
  }

  public async Task<Result> LogOut()
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.PostAsJsonAsync(HttpConstants.LogoutEndpoint, new { });
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> RemoveDeviceTag(Guid deviceId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.DeviceTagsEndpoint}/{deviceId}/{tagId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> RemoveUserRole(Guid userId, Guid roleId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UserRolesEndpoint}/{userId}/{roleId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result> RemoveUserTag(Guid userId, Guid tagId)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.DeleteAsync($"{HttpConstants.UserTagsEndpoint}/{userId}/{tagId}");
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result<TagResponseDto>> RenameTag(Guid tagId, string newTagName)
  {
    return await TryCallApi(async () =>
    {
      var dto = new TagRenameRequestDto(tagId, newTagName);
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.TagsEndpoint}", dto);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TagResponseDto>();
    });
  }

  public async Task<Result<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.PostAsJsonAsync($"{HttpConstants.DevicesEndpoint}/search", request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<DeviceSearchResponseDto>();
    });
  }

  public async Task<Result<UserPreferenceResponseDto>> SetUserPreference(string preferenceName, string preferenceValue)
  {
    return await TryCallApi(async () =>
    {
      var request = new UserPreferenceRequestDto(preferenceName, preferenceValue);
      using var response = await _client.PostAsJsonAsync(HttpConstants.UserPreferencesEndpoint, request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<UserPreferenceResponseDto>();
    });
  }

  public async Task<Result<TenantSettingResponseDto?>> GetTenantSetting(string settingName)
  {
    return await TryGetNullableResponse(async () =>
    {
      using var response = await _client.GetAsync($"{HttpConstants.TenantSettingsEndpoint}/{settingName}");
      if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
      {
        return null;
      }
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>();
    });
  }

  public async Task<Result<TenantSettingResponseDto>> SetTenantSetting(string settingName, string settingValue)
  {
    return await TryCallApi(async () =>
    {
      var request = new TenantSettingRequestDto(settingName, settingValue);
      using var response = await _client.PostAsJsonAsync(HttpConstants.TenantSettingsEndpoint, request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<TenantSettingResponseDto>();
    });
  }

  public async Task<Result> DeleteTenantSetting(string settingName)
  {
    return await TryCallApi(async () =>
    {
      var url = $"{HttpConstants.TenantSettingsEndpoint}/{settingName}";
      using var response = await _client.DeleteAsync(url);
      response.EnsureSuccessStatusCode();
    });
  }

  public async Task<Result<PersonalAccessTokenDto>> UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request)
  {
    return await TryCallApi(async () =>
    {
      using var response = await _client.PutAsJsonAsync($"{HttpConstants.PersonalAccessTokensEndpoint}/{personalAccessTokenId}", request);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<PersonalAccessTokenDto>();
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

  private async Task<Result<T>> TryCallApi<T>(Func<Task<Result<T>>> func)
  {
    try
    {
      return await func.Invoke();
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

  private async Task<Result<T?>> TryGetNullableResponse<T>(Func<Task<T?>> func)
  {
    try
    {
      var resultValue = await func.Invoke();
      return Result.Ok(resultValue);
    }
    catch (HttpRequestException ex)
    {
      return Result
        .Fail<T?>(ex, ex.Message)
        .Log(_logger);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<T?>(ex, "The request to the server failed.")
        .Log(_logger);
    }
  }
}
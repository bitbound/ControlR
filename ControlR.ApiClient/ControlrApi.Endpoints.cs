using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.ApiClient;

public interface IAgentUpdateApi
{
  Task<ApiResult<string>> GetCurrentAgentHashSha256(RuntimeId runtime, CancellationToken cancellationToken = default);
}

public interface IAuthApi
{
  Task<ApiResult> LogOut(CancellationToken cancellationToken = default);
}

public interface IDesktopPreviewApi
{
  Task<ApiResult<byte[]>> GetDesktopPreview(Guid deviceId, int targetProcessId, CancellationToken cancellationToken = default);
}

public interface IDeviceFileSystemApi
{
  Task<ApiResult> CreateDirectory(CreateDirectoryRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteFile(FileDeleteRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<ResponseStream>> DownloadFile(Guid deviceId, string filePath, CancellationToken cancellationToken = default);
  Task<ApiResult<GetDirectoryContentsResponseDto>> GetDirectoryContents(GetDirectoryContentsRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<string>> GetLogFileContents(Guid deviceId, GetLogFileContentsRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetLogFilesResponseDto>> GetLogFiles(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<PathSegmentsResponseDto>> GetPathSegments(GetPathSegmentsRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetRootDrivesResponseDto>> GetRootDrives(GetRootDrivesRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<GetSubdirectoriesResponseDto>> GetSubdirectories(GetSubdirectoriesRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> UploadFile(Guid deviceId, Stream fileStream, string fileName, string targetSaveDirectory, bool overwrite = false, CancellationToken cancellationToken = default);
  Task<ApiResult<ValidateFilePathResponseDto>> ValidateFilePath(ValidateFilePathRequestDto request, CancellationToken cancellationToken = default);
}

public interface IDeviceTagsApi
{
  Task<ApiResult> AddDeviceTag(DeviceTagAddRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> RemoveDeviceTag(Guid deviceId, Guid tagId, CancellationToken cancellationToken = default);
}

public interface IDevicesApi
{
  Task<ApiResult> CreateDevice(CreateDeviceRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteDevice(Guid deviceId, CancellationToken cancellationToken = default);
  IAsyncEnumerable<DeviceResponseDto> GetAllDevices(CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceResponseDto>> GetDevice(Guid deviceId, CancellationToken cancellationToken = default);
  Task<ApiResult<DeviceSearchResponseDto>> SearchDevices(DeviceSearchRequestDto request, CancellationToken cancellationToken = default);
}

public interface IInstallerKeysApi
{
  Task<ApiResult<CreateInstallerKeyResponseDto>> CreateInstallerKey(CreateInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteInstallerKey(Guid keyId, CancellationToken cancellationToken = default);
  Task<ApiResult<AgentInstallerKeyDto[]>> GetAllInstallerKeys(CancellationToken cancellationToken = default);
  Task<ApiResult<AgentInstallerKeyUsageDto[]>> GetInstallerKeyUsages(Guid keyId, CancellationToken cancellationToken = default);
  Task<ApiResult> IncrementInstallerKeyUsage(Guid keyId, Guid? deviceId = null, CancellationToken cancellationToken = default);
  Task<ApiResult> RenameInstallerKey(RenameInstallerKeyRequestDto dto, CancellationToken cancellationToken = default);
}

public interface IInvitesApi
{
  Task<ApiResult<AcceptInvitationResponseDto>> AcceptInvitation(AcceptInvitationRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantInviteResponseDto>> CreateTenantInvite(TenantInviteRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteTenantInvite(Guid inviteId, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantInviteResponseDto[]>> GetPendingTenantInvites(CancellationToken cancellationToken = default);
}

public interface ILogonTokensApi
{
  Task<ApiResult<LogonTokenResponseDto>> CreateLogonToken(LogonTokenRequestDto request, CancellationToken cancellationToken = default);
}

public interface IPersonalAccessTokensApi
{
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreatePersonalAccessToken(CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeletePersonalAccessToken(Guid personalAccessTokenId, CancellationToken cancellationToken = default);
  Task<ApiResult<PersonalAccessTokenDto[]>> GetPersonalAccessTokens(CancellationToken cancellationToken = default);
  Task<ApiResult<PersonalAccessTokenDto>> UpdatePersonalAccessToken(Guid personalAccessTokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}

public interface IPublicRegistrationSettingsApi
{
  Task<ApiResult<PublicRegistrationSettings>> GetPublicRegistrationSettings(CancellationToken cancellationToken = default);
}

public interface IRolesApi
{
  Task<ApiResult<RoleResponseDto[]>> GetAllRoles(CancellationToken cancellationToken = default);
}

public interface IServerAlertApi
{
  Task<ApiResult<ServerAlertResponseDto>> GetServerAlert(CancellationToken cancellationToken = default);
  Task<ApiResult<ServerAlertResponseDto>> UpdateServerAlert(ServerAlertRequestDto request, CancellationToken cancellationToken = default);
}

public interface IServerLogsApi
{
  Task<ApiResult<GetAspireUrlResponseDto>> GetAspireUrl(CancellationToken cancellationToken = default);
}

public interface IServerStatsApi
{
  Task<ApiResult<ServerStatsDto>> GetServerStats(CancellationToken cancellationToken = default);
}

public interface ITagsApi
{
  Task<ApiResult<TagResponseDto>> CreateTag(TagCreateRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteTag(Guid tagId, CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto[]>> GetAllTags(bool includeLinkedIds = false, CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto>> RenameTag(TagRenameRequestDto request, CancellationToken cancellationToken = default);
}

public interface ITenantSettingsApi
{
  Task<ApiResult> DeleteTenantSetting(string settingName, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingResponseDto>> GetTenantSetting(string settingName, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingResponseDto[]>> GetTenantSettings(CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingResponseDto>> SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken = default);
}

public interface ITestEmailApi
{
  Task<ApiResult> SendTestEmail(CancellationToken cancellationToken = default);
}

public interface IUserPreferencesApi
{
  Task<ApiResult<UserPreferenceResponseDto>> GetUserPreference(string preferenceName, CancellationToken cancellationToken = default);
  Task<ApiResult<UserPreferenceResponseDto[]>> GetUserPreferences(CancellationToken cancellationToken = default);
  Task<ApiResult<UserPreferenceResponseDto>> SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken = default);
}

public interface IUserRolesApi
{
  Task<ApiResult> AddUserRole(UserRoleAddRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<RoleResponseDto[]>> GetOwnRoles(CancellationToken cancellationToken = default);
  Task<ApiResult<RoleResponseDto[]>> GetUserRoles(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult> RemoveUserRole(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}

public interface IUserServerSettingsApi
{
  Task<ApiResult<long>> GetFileUploadMaxSize(CancellationToken cancellationToken = default);
}

public interface IUserTagsApi
{
  Task<ApiResult> AddUserTag(UserTagAddRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto[]>> GetAllowedTags(CancellationToken cancellationToken = default);
  Task<ApiResult<TagResponseDto[]>> GetUserTags(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult> RemoveUserTag(Guid userId, Guid tagId, CancellationToken cancellationToken = default);
}

public interface IUsersApi
{
  Task<ApiResult<UserResponseDto>> CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult<UserResponseDto[]>> GetAllUsers(CancellationToken cancellationToken = default);
}

public interface IAgentVersionApi
{
  Task<ApiResult<Version>> GetCurrentAgentVersion(CancellationToken cancellationToken = default);
}

public interface IServerVersionApi
{
  Task<ApiResult<Version>> GetCurrentServerVersion(CancellationToken cancellationToken = default);
}

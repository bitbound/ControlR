using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITenantSettingsApi
{
  [ApiRoute("DELETE", "/api/internal/tenant-settings/{settingName}")]
  Task<ApiResult> DeleteTenantSetting(string settingName, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/tenant-settings/{settingName}")]
  Task<ApiResult<TenantSettingResponseDto>> GetTenantSetting(string settingName, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/tenant-settings")]
  Task<ApiResult<TenantSettingsDto>> GetTenantSettings(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/tenant-settings")]
  Task<ApiResult<TenantSettingResponseDto>> SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/internal/tenant-settings")]
  Task<ApiResult<TenantSettingsDto>> SetTenantSettings(TenantSettingsDto request, CancellationToken cancellationToken = default);
}

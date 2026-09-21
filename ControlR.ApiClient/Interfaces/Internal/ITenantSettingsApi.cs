using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITenantSettingsApi
{
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}/{{settingName}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.TenantSettings.DeleteTenantSetting (DELETE /api/v1/tenant-settings/{settingName}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteTenantSetting(string settingName, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}/{{settingName}}", "GET")]
  [Obsolete("Use ControlrApi.V1.TenantSettings.GetTenantSetting (GET /api/v1/tenant-settings/{settingName}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TenantSettingResponseDto>> GetTenantSetting(string settingName, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.TenantSettings.GetTenantSettings (GET /api/v1/tenant-settings?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TenantSettingsDto>> GetTenantSettings(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.TenantSettings.SetTenantSetting (POST /api/v1/tenant-settings?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TenantSettingResponseDto>> SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}", "PUT")]
  [Obsolete("Use ControlrApi.V1.TenantSettings.SetTenantSettings (PUT /api/v1/tenant-settings?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<TenantSettingsDto>> SetTenantSettings(TenantSettingsDto request, CancellationToken cancellationToken = default);
}

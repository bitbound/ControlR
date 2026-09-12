using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ITenantSettingsApi
{
  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}/{{settingName}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteTenantSetting(string settingName, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}/{{settingName}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<TenantSettingResponseDto>> GetTenantSetting(string settingName, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<TenantSettingsDto>> GetTenantSettings(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<TenantSettingResponseDto>> SetTenantSetting(Guid tenantId, TenantSettingRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<TenantSettingsDto>> SetTenantSettings(Guid tenantId, TenantSettingsDto request, CancellationToken cancellationToken = default);
}

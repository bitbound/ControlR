using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using SettingsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ITenantSettingsApi
{
  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}/{{settingName}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteTenantSetting(string settingName, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}/{{settingName}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<SettingsDtos.TenantSettingResponseDto>> GetTenantSetting(string settingName, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<SettingsDtos.TenantSettingsDto>> GetTenantSettings(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<SettingsDtos.TenantSettingResponseDto>> SetTenantSetting(Guid tenantId, SettingsDtos.TenantSettingRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.TenantSettingsEndpoint}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<SettingsDtos.TenantSettingsDto>> SetTenantSettings(Guid tenantId, SettingsDtos.TenantSettingsDto request, CancellationToken cancellationToken = default);
}

using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITenantSettingsApi
{
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}/{{settingName}}", "DELETE")]
  Task<ApiResult> DeleteTenantSetting(string settingName, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}/{{settingName}}", "GET")]
  Task<ApiResult<TenantSettingResponseDto>> GetTenantSetting(string settingName, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}", "GET")]
  Task<ApiResult<TenantSettingsDto>> GetTenantSettings(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}", "POST")]
  Task<ApiResult<TenantSettingResponseDto>> SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.TenantSettingsEndpoint}", "PUT")]
  Task<ApiResult<TenantSettingsDto>> SetTenantSettings(TenantSettingsDto request, CancellationToken cancellationToken = default);
}

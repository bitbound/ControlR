using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface ITenantSettingsApi
{
  Task<ApiResult> DeleteTenantSetting(string settingName, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingResponseDto>> GetTenantSetting(string settingName, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingsDto>> GetTenantSettings(CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingResponseDto>> SetTenantSetting(TenantSettingRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<TenantSettingsDto>> SetTenantSettings(TenantSettingsDto request, CancellationToken cancellationToken = default);
}

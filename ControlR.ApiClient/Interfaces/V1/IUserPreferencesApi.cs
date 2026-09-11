using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using PrefsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}/{{name}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PrefsDtos.UserPreferenceResponseDto>> GetPreference(string name, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PrefsDtos.UserPreferencesDto>> GetPreferences(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<PrefsDtos.UserPreferenceResponseDto>> SetPreference(Guid tenantId, PrefsDtos.UserPreferenceRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<PrefsDtos.UserPreferencesDto>> SetPreferences(Guid tenantId, PrefsDtos.UserPreferencesDto request, CancellationToken cancellationToken = default);
}

using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.UserPreferences;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}/{{name}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UserPreferenceResponseDto>> GetPreference(string name, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UserPreferencesDto>> GetPreferences(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<UserPreferenceResponseDto>> SetPreference(Guid tenantId, UserPreferenceRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UserPreferencesEndpoint}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<UserPreferencesDto>> SetPreferences(Guid tenantId, UserPreferencesDto request, CancellationToken cancellationToken = default);
}

using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}/{{preferenceName}}", "GET")]
  Task<ApiResult<UserPreferenceResponseDto>> GetUserPreference(string preferenceName, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}", "GET")]
  Task<ApiResult<UserPreferencesDto>> GetUserPreferences(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}", "POST")]
  Task<ApiResult<UserPreferenceResponseDto>> SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}", "PUT")]
  Task<ApiResult<UserPreferencesDto>> SetUserPreferences(UserPreferencesDto request, CancellationToken cancellationToken = default);
}

using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserPreferencesApi
{
  [ApiRoute("GET", "/api/internal/user-preferences/{preferenceName}")]
  Task<ApiResult<UserPreferenceResponseDto>> GetUserPreference(string preferenceName, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/user-preferences")]
  Task<ApiResult<UserPreferencesDto>> GetUserPreferences(CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/user-preferences")]
  Task<ApiResult<UserPreferenceResponseDto>> SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/internal/user-preferences")]
  Task<ApiResult<UserPreferencesDto>> SetUserPreferences(UserPreferencesDto request, CancellationToken cancellationToken = default);
}

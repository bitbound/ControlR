using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserPreferencesApi
{
  Task<ApiResult<UserPreferenceResponseDto>> GetUserPreference(string preferenceName, CancellationToken cancellationToken = default);
  Task<ApiResult<UserPreferencesDto>> GetUserPreferences(CancellationToken cancellationToken = default);
  Task<ApiResult<UserPreferenceResponseDto>> SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<UserPreferencesDto>> SetUserPreferences(UserPreferencesDto request, CancellationToken cancellationToken = default);
}

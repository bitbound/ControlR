using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}/{{preferenceName}}", "GET")]
  [Obsolete("Use ControlrApi.V1.UserPreferences.GetPreference (GET /api/v1/user-preferences/{name}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserPreferenceResponseDto>> GetUserPreference(string preferenceName, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}", "GET")]
  Task<ApiResult<UserPreferencesDto>> GetUserPreferences(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.UserPreferences.SetPreference (POST /api/v1/user-preferences?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserPreferenceResponseDto>> SetUserPreference(UserPreferenceRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UserPreferencesEndpoint}", "PUT")]
  [Obsolete("Use ControlrApi.V1.UserPreferences.SetPreferences (PUT /api/v1/user-preferences?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserPreferencesDto>> SetUserPreferences(UserPreferencesDto request, CancellationToken cancellationToken = default);
}

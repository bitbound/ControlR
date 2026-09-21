using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IEffectiveUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.Internal.EffectiveUserPreferencesEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.EffectiveUserPreferences.GetEffectiveUserPreferences (GET /api/v1/effective-user-preferences?tenantId=), which requires an explicit tenantId. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<EffectiveUserPreferencesDto>> GetEffectiveUserPreferences(CancellationToken cancellationToken = default);
}

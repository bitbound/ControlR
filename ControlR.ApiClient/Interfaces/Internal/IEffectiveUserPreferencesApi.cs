using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IEffectiveUserPreferencesApi
{
  [ApiRoute("GET", "/api/internal/effective-user-preferences")]
  Task<ApiResult<EffectiveUserPreferencesDto>> GetEffectiveUserPreferences(CancellationToken cancellationToken = default);
}

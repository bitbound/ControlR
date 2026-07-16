using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IEffectiveUserPreferencesApi
{
  [ApiRoute($"{HttpConstants.Internal.EffectiveUserPreferencesEndpoint}", "GET")]
  Task<ApiResult<EffectiveUserPreferencesDto>> GetEffectiveUserPreferences(CancellationToken cancellationToken = default);
}

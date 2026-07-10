using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IEffectiveUserPreferencesApi
{
  Task<ApiResult<EffectiveUserPreferencesDto>> GetEffectiveUserPreferences(CancellationToken cancellationToken = default);
}

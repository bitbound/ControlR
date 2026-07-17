using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPublicServerSettingsApi
{
  [ApiRoute($"{HttpConstants.Internal.PublicServerSettingsEndpoint}", "GET")]
  Task<ApiResult<PublicServerSettings>> GetPublicServerSettings(CancellationToken cancellationToken = default);
}

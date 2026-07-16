using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IPublicRegistrationSettingsApi
{
  [ApiRoute($"{HttpConstants.Internal.PublicRegistrationSettingsEndpoint}", "GET")]
  Task<ApiResult<PublicRegistrationSettings>> GetPublicRegistrationSettings(CancellationToken cancellationToken = default);
}

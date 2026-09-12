using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PublicServerSettings;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IPublicServerSettingsApi
{
  [ApiRoute($"{HttpConstants.V1.PublicServerSettingsEndpoint}", "GET")]
  Task<ApiResult<PublicServerSettingsDto>> GetPublicServerSettings(CancellationToken cancellationToken = default);
}

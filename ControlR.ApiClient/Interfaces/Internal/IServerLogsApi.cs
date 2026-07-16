using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerLogsApi
{
  [ApiRoute($"{HttpConstants.Internal.ServerLogsEndpoint}/get-aspire-url", "GET")]
  Task<ApiResult<GetAspireUrlResponseDto>> GetAspireUrl(CancellationToken cancellationToken = default);
}

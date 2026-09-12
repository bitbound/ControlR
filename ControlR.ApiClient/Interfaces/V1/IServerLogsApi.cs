using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerLogs;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IServerLogsApi
{
  [ApiRoute($"{HttpConstants.V1.ServerLogsEndpoint}/get-aspire-url", "GET")]
  Task<ApiResult<GetAspireUrlResponseDto>> GetAspireUrl(CancellationToken cancellationToken = default);
}

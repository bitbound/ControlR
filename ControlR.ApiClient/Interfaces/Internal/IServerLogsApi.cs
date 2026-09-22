using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IServerLogsApi
{
  [ApiRoute($"{HttpConstants.Internal.ServerLogsEndpoint}/get-aspire-url", "GET")]
  [Obsolete("Use ControlrApi.V1.ServerLogs.GetAspireUrl (GET /api/v1/server-logs/get-aspire-url), which returns the same value. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<GetAspireUrlResponseDto>> GetAspireUrl(CancellationToken cancellationToken = default);
}

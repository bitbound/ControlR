using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IAuthorizationChangeLogsApi
{
  [ApiRoute(HttpConstants.Internal.AuthorizationChangeLogsEndpoint, "GET")]
  Task<ApiResult<AuthorizationChangeLogSearchResponseDto>> Get(
    int page = 0,
    int pageSize = 50,
    string? actionType = null,
    string? actorType = null,
    string? targetType = null,
    string? searchText = null,
    Guid? tenantId = null,
    DateTimeOffset? from = null,
    DateTimeOffset? to = null,
    CancellationToken cancellationToken = default);
}

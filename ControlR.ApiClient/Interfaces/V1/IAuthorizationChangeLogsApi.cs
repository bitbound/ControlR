using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ACLDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IAuthorizationChangeLogsApi
{
  [ApiRoute($"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<ACLDtos.AuthorizationChangeLogsResponseDto>> GetAuthorizationChangeLogs(
    Guid tenantId,
    int page = 0,
    int pageSize = 50,
    string? actionType = null,
    string? actorType = null,
    string? targetType = null,
    string? searchText = null,
    DateTimeOffset? from = null,
    DateTimeOffset? to = null,
    CancellationToken cancellationToken = default);
}
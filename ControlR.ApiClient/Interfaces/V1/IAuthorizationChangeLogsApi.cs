using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IAuthorizationChangeLogsApi
{
  [ApiRoute($"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<AuthorizationChangeLogsResponseDto>> GetAuthorizationChangeLogs(
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

  // Sub-route of the authorization-change-logs resource, so it composes from the parent
  // endpoint rather than carrying its own HttpConstants entry (an extra *Endpoint constant must
  // map to an IControlrApi sub-client property, which a sub-route cannot).
  [ApiRoute($"{HttpConstants.V1.AuthorizationChangeLogsEndpoint}/server", "GET")]
  Task<ApiResult<AuthorizationChangeLogsResponseDto>> GetServerAuthorizationChangeLogs(
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
using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.AuthorizationChangeLogs;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Authorization change log inspection. The read audience is the union of two disjoint
/// permissions: holders of server.authorization-logs.read (server scope) inspect the tenant
/// named by the required tenantId query parameter, and holders of tenant.authorization-logs.read
/// (tenant scope) inspect their own tenant. No single authorization policy models that union,
/// so the audience check runs in the handler (evaluating both permissions, as the superseded
/// internal endpoint did) rather than as a method-level policy.
/// Server-scoped entries (OwningTenantId is null) belong to no tenant and are therefore not
/// reachable through the tenant-addressed list; they are served by the separate, parameterless
/// GET /server route, which is restricted to server principals without tenant context.
/// </summary>
[Route(HttpConstants.V1.AuthorizationChangeLogsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class AuthorizationChangeLogsController(
  AppDb appDb,
  IPermissionEvaluator permissionEvaluator) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;

  [HttpGet]
  [ProducesResponseType<AuthorizationChangeLogsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AuthorizationChangeLogsResponseDto>> Get(
    [FromQuery] Guid tenantId,
    [FromQuery] AuthorizationChangeLogSearchQueryDto searchQuery,
    CancellationToken cancellationToken)
  {
    var principal = User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return BadRequest("User principal not found.");
    }

    var serverResource = new ResourceDescriptor(PermissionScopeKind.Server);
    var requestServer = new PermissionEvaluationRequest(
      PermissionNames.ServerAuthorizationLogsRead,
      serverResource);

    var requestTenant = principal.TenantId.HasValue
      ? new PermissionEvaluationRequest(
          PermissionNames.TenantAuthorizationLogsRead,
          new ResourceDescriptor(
            PermissionScopeKind.Tenant,
            principal.TenantId.Value,
            principal.TenantId.Value))
      : null;

    PermissionEvaluationRequest[]? requests = requestTenant is null
      ? [requestServer]
      : [requestServer, requestTenant];

    var decisions = await _permissionEvaluator.EvaluateBatch(
      principal,
      requests,
      cancellationToken);

    var canReadServer = decisions[requestServer].Allowed;
    var canReadTenant = requestTenant is not null && decisions[requestTenant].Allowed;

    if (!canReadServer && !canReadTenant)
    {
      return Forbid();
    }

    Guid scopedTenantId;
    if (canReadServer)
    {
      // Server-scoped readers hold server.authorization-logs.read, which authorizes inspecting
      // any tenant; the required tenantId query parameter is the tenant they select.
      scopedTenantId = tenantId;
    }
    else
    {
      // Tenant-scoped readers see their own tenant only.
      if (!User.TryResolveTenantId(tenantId, out var callerTenantId))
      {
        return Forbid();
      }

      scopedTenantId = callerTenantId;
    }

    var query = ApplySearchFilters(
      _appDb.AuthorizationChangeLogs.AsNoTracking().Where(x => x.OwningTenantId == scopedTenantId),
      searchQuery);

    return Ok(await BuildResponseAsync(query, searchQuery, cancellationToken));
  }

  /// <summary>
  /// Lists server-scoped audit entries (OwningTenantId is null): server service-account edits,
  /// server administrator grants, and other changes that belong to no tenant. The route takes
  /// no tenantId - the rows it serves have no tenant - and refuses any caller carrying a tenant
  /// claim before permission evaluation, so the server-scoped view is exclusively the domain of
  /// server principals holding server.authorization-logs.read.
  /// </summary>
  [HttpGet("server")]
  [ProducesResponseType<AuthorizationChangeLogsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AuthorizationChangeLogsResponseDto>> GetServerScoped(
    [FromQuery] AuthorizationChangeLogSearchQueryDto searchQuery,
    CancellationToken cancellationToken)
  {
    // Fail early for tenant-context principals: this endpoint answers only for callers that act
    // across tenants, never for principals bound to one.
    if (User.TryGetTenantId(out _))
    {
      return Forbid();
    }

    var principal = User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return BadRequest("User principal not found.");
    }

    var requestServer = new PermissionEvaluationRequest(
      PermissionNames.ServerAuthorizationLogsRead,
      new ResourceDescriptor(PermissionScopeKind.Server));

    var decisions = await _permissionEvaluator.EvaluateBatch(
      principal,
      [requestServer],
      cancellationToken);

    if (!decisions[requestServer].Allowed)
    {
      return Forbid();
    }

    var query = ApplySearchFilters(
      _appDb.AuthorizationChangeLogs.AsNoTracking().Where(x => x.OwningTenantId == null),
      searchQuery);

    return Ok(await BuildResponseAsync(query, searchQuery, cancellationToken));
  }

  private IQueryable<AuthorizationChangeLog> ApplySearchFilters(
    IQueryable<AuthorizationChangeLog> query,
    AuthorizationChangeLogSearchQueryDto searchQuery)
  {
    if (!string.IsNullOrWhiteSpace(searchQuery.ActionType))
    {
      query = query.Where(x => x.ActionType == searchQuery.ActionType);
    }

    if (!string.IsNullOrWhiteSpace(searchQuery.TargetType))
    {
      query = query.Where(x => x.TargetType == searchQuery.TargetType);
    }

    if (!string.IsNullOrWhiteSpace(searchQuery.ActorType))
    {
      query = query.Where(x => x.ActorPrincipalType == searchQuery.ActorType);
    }

    if (!string.IsNullOrWhiteSpace(searchQuery.SearchText))
    {
      var trimmed = searchQuery.SearchText.Trim();

      // Exact GUID lookup when the query parses as a full UUID.
      if (Guid.TryParse(trimmed, out var parsedGuid))
      {
        query = query.Where(x =>
          x.ActorPrincipalId == parsedGuid ||
          x.TargetId == parsedGuid);
      }
      else if (_appDb.Database.IsRelational())
      {
        // Partial ID query: match against the canonical text form of the UUID,
        // case-insensitively (ILIKE). Escape LIKE wildcards so user input such as '%' or '_'
        // is matched literally instead of acting as a wildcard. ILIKE is PostgreSQL syntax;
        // PostgreSQL is this application's only relational provider.
        var escaped = trimmed
          .Replace("\\", "\\\\")
          .Replace("%", "\\%")
          .Replace("_", "\\_");
        query = query.Where(x =>
          (x.ActorPrincipalId != null && EF.Functions.ILike(x.ActorPrincipalId.Value.ToString(), $"%{escaped}%")) ||
          (x.TargetId != null && EF.Functions.ILike(x.TargetId.Value.ToString(), $"%{escaped}%")));
      }
      else
      {
        // The in-memory provider has no ILIKE translation; compare lowercased text instead.
        var lowered = trimmed.ToLower();
        query = query.Where(x =>
          (x.ActorPrincipalId != null && x.ActorPrincipalId.Value.ToString().ToLower().Contains(lowered)) ||
          (x.TargetId != null && x.TargetId.Value.ToString().ToLower().Contains(lowered)));
      }
    }

    if (searchQuery.From.HasValue)
    {
      query = query.Where(x => x.CreatedAt >= searchQuery.From.Value);
    }

    if (searchQuery.To.HasValue)
    {
      query = query.Where(x => x.CreatedAt <= searchQuery.To.Value);
    }

    return query;
  }

  private async Task<AuthorizationChangeLogsResponseDto> BuildResponseAsync(
    IQueryable<AuthorizationChangeLog> query,
    AuthorizationChangeLogSearchQueryDto searchQuery,
    CancellationToken cancellationToken)
  {
    var totalItems = await query.CountAsync(cancellationToken);

    var clampedPageSize = Math.Clamp(searchQuery.PageSize, 1, DtoLimits.AuthorizationChangeLogMaxPageSize);
    // Clamp the page so the skip multiplication cannot overflow int (which would
    // produce a negative SQL OFFSET and fail the query).
    var clampedPage = Math.Clamp(searchQuery.Page, 0, int.MaxValue / clampedPageSize);
    var items = await query
      .OrderByDescending(x => x.CreatedAt)
      .Skip(clampedPage * clampedPageSize)
      .Take(clampedPageSize)
      .Select(x => new AuthorizationChangeLogDto(
        x.Id,
        x.ActionType,
        x.ActorPrincipalType,
        x.ActorPrincipalId,
        x.TargetType,
        x.TargetId,
        x.OwningTenantId,
        x.IpAddress,
        x.CreatedAt,
        x.BeforeJson,
        x.AfterJson))
      .ToListAsync(cancellationToken);

    return new AuthorizationChangeLogsResponseDto
    {
      Items = items,
      TotalItems = totalItems
    };
  }
}

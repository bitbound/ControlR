using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

/// <summary>
/// Authorization change log inspection. Holders of server.authorization-logs.read see all
/// entries (optionally filtered by tenant); holders of tenant.authorization-logs.read see
/// only their own tenant's entries. Entries with no owning tenant (server-scoped changes)
/// are visible to server.authorization-logs.read holders only.
/// </summary>
[Route(HttpConstants.Internal.AuthorizationChangeLogsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class AuthorizationChangeLogsController : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<InternalDtos.AuthorizationChangeLogSearchResponseDto>> Get(
    [FromServices] AppDb appDb,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromQuery] InternalDtos.AuthorizationChangeLogSearchQueryDto searchQuery,
    CancellationToken cancellationToken = default)
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

    var decisions = await permissionEvaluator.EvaluateBatch(
      principal,
      requests,
      cancellationToken);

    var canReadServer = decisions[requestServer].Allowed;
    var canReadTenant = requestTenant is not null && decisions[requestTenant].Allowed;

    if (!canReadServer && !canReadTenant)
    {
      return Forbid();
    }

    Guid? scopedTenantId;
    if (canReadServer)
    {
      scopedTenantId = searchQuery.TenantId;
    }
    else
    {
      if (!User.TryGetTenantId(out var callerTenantId))
      {
        return BadRequest("User tenant not found.");
      }

      if (searchQuery.TenantId.HasValue && searchQuery.TenantId.Value != callerTenantId)
      {
        return Forbid();
      }

      scopedTenantId = callerTenantId;
    }

    var query = appDb.AuthorizationChangeLogs.AsNoTracking();

    if (scopedTenantId is { } scopeTenant)
    {
      query = query.Where(x => x.OwningTenantId == scopeTenant);
    }

    if (!string.IsNullOrWhiteSpace(searchQuery.ActionType))
    {
      query = query.Where(x => x.ActionType == searchQuery.ActionType);
    }

    if (!string.IsNullOrWhiteSpace(searchQuery.TargetType))
    {
      query = query.Where(x => x.TargetType == searchQuery.TargetType);
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
      else
      {
        // Partial ID query: match against the canonical text form of the UUID,
        // case-insensitively (ILIKE). Escape LIKE wildcards so user input such as '%' or '_'
        // is matched literally instead of acting as a wildcard.
        var escaped = trimmed
          .Replace("\\", "\\\\")
          .Replace("%", "\\%")
          .Replace("_", "\\_");
        query = query.Where(x =>
          (x.ActorPrincipalId != null && EF.Functions.ILike(x.ActorPrincipalId.Value.ToString(), $"%{escaped}%")) ||
          (x.TargetId != null && EF.Functions.ILike(x.TargetId.Value.ToString(), $"%{escaped}%")));
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

    var totalItems = await query.CountAsync(cancellationToken);

    var clampedPageSize = Math.Clamp(searchQuery.PageSize, 1, DtoLimits.AuthorizationChangeLogMaxPageSize);
    // Clamp the page so the skip multiplication cannot overflow int (which would
    // produce a negative SQL OFFSET and fail the query).
    var clampedPage = Math.Clamp(searchQuery.Page, 0, int.MaxValue / clampedPageSize);
    var items = await query
      .OrderByDescending(x => x.CreatedAt)
      .Skip(clampedPage * clampedPageSize)
      .Take(clampedPageSize)
      .Select(x => new InternalDtos.AuthorizationChangeLogDto(
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

    return Ok(new InternalDtos.AuthorizationChangeLogSearchResponseDto(items, totalItems));
  }
}

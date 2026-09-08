using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.EffectivePermissionsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class EffectivePermissionsController(IPermissionEvaluator permissionEvaluator) : ControllerBase
{
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;

  [HttpPost("query")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsRead)]
  public async Task<ActionResult<InternalDtos.EffectivePermissionQueryResponseDto>> Query(
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.EffectivePermissionQueryRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (request.PrincipalKind is not (PermissionPrincipalKind.User or
                                      PermissionPrincipalKind.UserGroup or
                                      PermissionPrincipalKind.ServiceAccount))
    {
      return BadRequest("Unsupported principal kind.");
    }

    // ServiceAccounts has no claims-driven query filter, so its predicate is the only tenant
    // guard here. Server service accounts are excluded (server-level configuration, not tenant business).
    if (!await PrincipalExistsInTenant(appDb, request.PrincipalKind, request.PrincipalId, tenantId, cancellationToken))
    {
      return NotFound("Principal not found in this tenant.");
    }

    var principal = new PrincipalDescriptor(
      PrincipalType: request.PrincipalKind switch
      {
        PermissionPrincipalKind.User => PrincipalType.User,
        PermissionPrincipalKind.UserGroup => PrincipalType.UserGroup,
        PermissionPrincipalKind.ServiceAccount => PrincipalType.TenantServiceAccount,
        _ => throw new InvalidOperationException("Unsupported principal kind.")
      },
      PrincipalId: request.PrincipalId,
      TenantId: tenantId,
      AuthMethod: "effective-permission-query");

    var resource = new ResourceDescriptor(request.ScopeKind, request.ScopeId, tenantId);

    var result = await _permissionEvaluator.Evaluate(
      principal, request.PermissionName, resource, cancellationToken);

    return Ok(new InternalDtos.EffectivePermissionQueryResponseDto(
      result.Allowed,
      result.Allowed ? null : result.DenialReason ?? "Permission denied by policy evaluation."));
  }

  private static Task<bool> PrincipalExistsInTenant(
    AppDb appDb,
    PermissionPrincipalKind principalKind,
    Guid principalId,
    Guid tenantId,
    CancellationToken cancellationToken) => principalKind switch
    {
      PermissionPrincipalKind.User => appDb.Users
        .AnyAsync(x => x.Id == principalId && x.TenantId == tenantId, cancellationToken),
      PermissionPrincipalKind.UserGroup => appDb.UserGroups
        .AnyAsync(x => x.Id == principalId && x.TenantId == tenantId, cancellationToken),
      PermissionPrincipalKind.ServiceAccount => appDb.ServiceAccounts
        .AnyAsync(x => x.Id == principalId &&
                       x.Kind == ServiceAccountKind.Tenant &&
                       x.TenantId == tenantId, cancellationToken),
      _ => Task.FromResult(false)
    };
}

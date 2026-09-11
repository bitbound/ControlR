using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectivePermissions;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.EffectivePermissionsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class EffectivePermissionsController(
  AppDb appDb,
  IPermissionEvaluator permissionEvaluator) : ControllerBase
{
  private readonly AppDb _appDb = appDb;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;

  [HttpGet("{principalId:guid}")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsRead)]
  [ProducesResponseType<EffectivePermissionQueryResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<EffectivePermissionQueryResponseDto>> GetEffectivePermission(
    Guid principalId,
    [FromQuery] Guid tenantId,
    [FromQuery] PermissionPrincipalKind principalKind,
    [FromQuery] string permissionName,
    [FromQuery] PermissionScopeKind scopeKind,
    [FromQuery] Guid? scopeId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (principalKind is not (PermissionPrincipalKind.User or
                              PermissionPrincipalKind.UserGroup or
                              PermissionPrincipalKind.ServiceAccount))
    {
      return BadRequest("Unsupported principal kind.");
    }

    if (string.IsNullOrWhiteSpace(permissionName) || permissionName.Length > 150)
    {
      return BadRequest("Permission name is required and must be 150 characters or fewer.");
    }

    // ServiceAccounts has no claims-driven query filter, so this predicate is the only tenant
    // guard for that kind. Server service accounts are excluded (server-level configuration,
    // not tenant business). A principal in another tenant is indistinguishable from one that
    // does not exist so the endpoint cannot act as an existence oracle.
    if (!await PrincipalExistsInTenant(_appDb, principalKind, principalId, resolvedTenantId, cancellationToken) ||
        !User.CanAccessTenant(resolvedTenantId))
    {
      return NotFound();
    }

    var principal = new PrincipalDescriptor(
      PrincipalType: principalKind switch
      {
        PermissionPrincipalKind.User => PrincipalType.User,
        PermissionPrincipalKind.UserGroup => PrincipalType.UserGroup,
        PermissionPrincipalKind.ServiceAccount => PrincipalType.TenantServiceAccount,
        _ => throw new InvalidOperationException("Unsupported principal kind.")
      },
      PrincipalId: principalId,
      TenantId: resolvedTenantId,
      AuthMethod: "effective-permission-query");

    var resource = new ResourceDescriptor(scopeKind, scopeId, resolvedTenantId);

    var result = await _permissionEvaluator.Evaluate(
      principal, permissionName, resource, cancellationToken);

    return Ok(new EffectivePermissionQueryResponseDto(
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

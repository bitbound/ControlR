using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Permission assignment management. Tenant scoping is enforced by the required tenantId query
/// parameter. The caller's tenant claim must match it (or the caller must be a server
/// principal), and the resolved id is passed into every manager call in place of the claims
/// tenant id the superseded internal endpoint used. The manager's own write-authority and
/// tenant checks (ValidateWriteAuthority, IsVisibleToTenant, ValidatePermissionScope,
/// ValidatePrincipalExists) are untouched. Server-scope writes still require the
/// server.permissions.write grant and tenant-scoped targets are validated against the resolved
/// tenant.
/// </summary>
[Route(HttpConstants.V1.PermissionAssignmentsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class PermissionAssignmentsController(
  IPermissionAssignmentManager permissionAssignmentManager) : ControllerBase
{
  private readonly IPermissionAssignmentManager _permissionAssignmentManager = permissionAssignmentManager;

  [HttpPost("presets/apply")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType<int>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<int>> ApplyPresets(
    [FromQuery] Guid tenantId,
    [FromBody] ApplyPermissionPresetsRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.ApplyPresets(
      new InternalDtos.ApplyPermissionPresetsRequestDto(
        request.PrincipalKind,
        request.PrincipalId,
        request.PresetNames,
        request.ReplaceExisting),
      resolvedTenantId,
      actor,
      cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(result.Value);
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType<PermissionAssignmentDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PermissionAssignmentDto>> Create(
    [FromQuery] Guid tenantId,
    [FromBody] CreatePermissionAssignmentRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.Create(
      new InternalDtos.CreatePermissionAssignmentRequestDto(
        request.PrincipalKind,
        request.PrincipalId,
        request.PermissionName,
        request.Effect,
        request.ScopeKind,
        request.ScopeId,
        request.Notes,
        request.IsEnabled),
      resolvedTenantId,
      actor,
      cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return CreatedAtAction(
      nameof(GetByPrincipal),
      new
      {
        tenantId = resolvedTenantId,
        principalKind = request.PrincipalKind,
        principalId = request.PrincipalId
      },
      ToV1Dto(result.Value));
  }

  [HttpPost("batch")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> CreateMany(
    [FromQuery] Guid tenantId,
    [FromBody] CreateManyPermissionAssignmentsRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.CreateMany(
      [.. request.Assignments.Select(ToInternalCreateRequest)],
      resolvedTenantId,
      actor,
      cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpDelete("{assignmentId:guid}")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid assignmentId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.CanAccessTenant(resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.Delete(
      assignmentId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPost("batch-delete")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType<DeleteManyPermissionAssignmentsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DeleteManyPermissionAssignmentsResponseDto>> DeleteMany(
    [FromQuery] Guid tenantId,
    [FromBody] DeleteManyPermissionAssignmentsRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.DeleteMany(
      request.AssignmentIds, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(new DeleteManyPermissionAssignmentsResponseDto(
      result.Value.SuccessIds,
      result.Value.FailureIds));
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsRead)]
  [ProducesResponseType<PermissionAssignmentsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PermissionAssignmentsResponseDto>> GetByPrincipal(
    [FromQuery] Guid tenantId,
    [FromQuery] PermissionPrincipalKind principalKind,
    [FromQuery] Guid principalId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.CanAccessTenant(resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var assignments = await _permissionAssignmentManager.GetByPrincipal(
      principalKind, principalId, resolvedTenantId, actor, cancellationToken);

    return Ok(new PermissionAssignmentsResponseDto
    {
      Items = [.. assignments.Select(ToV1Dto)]
    });
  }

  [HttpGet("catalog")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsRead)]
  [ProducesResponseType<PermissionCatalogResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PermissionCatalogResponseDto>> GetCatalog(
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var entries = PermissionCatalog.All.Values
      .Select(x => new PermissionCatalogEntryDto(
        x.Name, x.DisplayName, x.Description, x.AllowedScopeKinds, x.SelfRemovable))
      .OrderBy(x => x.DisplayName)
      .ToList();

    return Ok(new PermissionCatalogResponseDto
    {
      Items = entries
    });
  }

  [HttpGet("presets")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsRead)]
  [ProducesResponseType<PermissionPresetsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PermissionPresetsResponseDto>> GetPresets(
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out _))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var presets = PermissionPresets.All
      .Select(p => new PermissionPresetDto(p.Key, [.. p.Value]))
      .OrderBy(p => p.Name)
      .ToList();

    return Ok(new PermissionPresetsResponseDto
    {
      Items = presets
    });
  }

  [HttpPost("replace")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Replace(
    [FromQuery] Guid tenantId,
    [FromBody] ReplacePermissionAssignmentsRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.ReplaceForPrincipal(
      request.PrincipalKind,
      request.PrincipalId,
      resolvedTenantId,
      actor,
      [.. request.Assignments.Select(ToInternalCreateRequest)],
      cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPut("{assignmentId:guid}")]
  [Authorize(Policy = PolicyNames.RequirePermissionAssignmentsWrite)]
  [ProducesResponseType<PermissionAssignmentDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PermissionAssignmentDto>> Update(
    [FromRoute] Guid assignmentId,
    [FromQuery] Guid tenantId,
    [FromBody] UpdatePermissionAssignmentRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.CanAccessTenant(resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("Permission assignment context not found.");
    }

    var result = await _permissionAssignmentManager.Update(
      assignmentId,
      new InternalDtos.UpdatePermissionAssignmentRequestDto(
        request.PermissionName,
        request.Effect,
        request.ScopeKind,
        request.ScopeId,
        request.Notes,
        request.IsEnabled),
      resolvedTenantId,
      actor,
      cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  private static InternalDtos.CreatePermissionAssignmentRequestDto ToInternalCreateRequest(
    CreatePermissionAssignmentRequestDto request)
  {
    return new InternalDtos.CreatePermissionAssignmentRequestDto(
      request.PrincipalKind,
      request.PrincipalId,
      request.PermissionName,
      request.Effect,
      request.ScopeKind,
      request.ScopeId,
      request.Notes,
      request.IsEnabled);
  }

  private static PermissionAssignmentDto ToV1Dto(InternalDtos.PermissionAssignmentDto source)
  {
    return new PermissionAssignmentDto(
      source.Id,
      source.PrincipalKind,
      source.PrincipalId,
      source.PermissionName,
      source.Effect,
      source.ScopeKind,
      source.ScopeId,
      source.Notes,
      source.IsEnabled,
      source.CreatedAt);
  }

  // Manager failures pass through verbatim, as they did on the superseded internal endpoint.
  // The manager's Forbidden results are caller-authorization refusals (missing server-scope or
  // deny-effect grants, server service account targets), never cross-tenant probes. Collapsing
  // them to NotFound would hide actionable authorization feedback. Cross-tenant access is
  // already refused earlier: TryResolveTenantId, CanAccessTenant, and the manager's own
  // principal/scope tenant validations.
  private static ActionResult ToV1Failure<T>(HttpResult<T> result) =>
    ToV1Failure(result.ToHttpResult());

  private static ActionResult ToV1Failure(HttpResult result) => result.ToActionResult();
}
using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.AgentInstaller;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.InstallerKeysEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class InstallerKeysController(
  IAgentInstallerKeyManager installerKeyManager,
  IPermissionEvaluator permissionEvaluator,
  IResourceDescriptorFactory resourceFactory) : ControllerBase
{
  private readonly IAgentInstallerKeyManager _installerKeyManager = installerKeyManager;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireInstallerKeySelfWrite)]
  public async Task<ActionResult<V1Dtos.CreateInstallerKeyResponseDto>> Create(
      [FromBody] CreateInstallerKeyRequestDto request)
  {
    if (!User.TryGetPrincipalId(out var creatorId))
    {
      return Forbid();
    }

    if (!User.TryResolveTenantId(request.TenantId, out var tenantId))
    {
      return Forbid();
    }

    var internalDto = await _installerKeyManager.CreateKey(
        tenantId,
        creatorId,
        User.GetCreatorKind(),
        request.KeyType,
        request.AllowedUses,
        request.Expiration,
        request.FriendlyName);

    var dto = V1Dtos.CreateInstallerKeyResponseDto.From(internalDto);

    return Ok(dto);
  }

  [HttpDelete("{keyId:guid}")]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyAnyWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> Delete(
      Guid keyId,
      [FromQuery] Guid tenantId,
      CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.TryGetPrincipalId(out var callerPrincipalId))
    {
      return Unauthorized();
    }

    var isAdmin = await CanManageAllKeysAsync(resolvedTenantId, cancellationToken);
    var result = await _installerKeyManager.DeleteKey(keyId, callerPrincipalId, resolvedTenantId, isAdmin);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyAnyRead)]
  [ProducesResponseType<InstallerKeysResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<InstallerKeysResponseDto>> GetAll(
      [FromQuery] Guid tenantId,
      CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.TryGetPrincipalId(out var callerPrincipalId))
    {
      return Unauthorized();
    }

    var isAdmin = await CanReadAllKeysAsync(resolvedTenantId, cancellationToken);
    var keys = await _installerKeyManager.GetAllKeys(resolvedTenantId, callerPrincipalId, isAdmin);

    return Ok(new InstallerKeysResponseDto
    {
      Items = [.. keys.Select(ToV1Dto)]
    });
  }

  [HttpGet("{keyId:guid}/usages")]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyAnyRead)]
  [ProducesResponseType<InstallerKeyUsagesResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> GetUsages(
      Guid keyId,
      [FromQuery] Guid tenantId,
      CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.TryGetPrincipalId(out var callerPrincipalId))
    {
      return Unauthorized();
    }

    var isAdmin = await CanReadAllKeysAsync(resolvedTenantId, cancellationToken);
    var result = await _installerKeyManager.GetKeyUsages(keyId, callerPrincipalId, resolvedTenantId, isAdmin);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(new InstallerKeyUsagesResponseDto
    {
      Items = [.. result.Value.Select(ToV1Dto)]
    });
  }

  [HttpPut("{keyId:guid}")]
  [Authorize(Policy = PolicyNames.RequireInstallerKeyAnyWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> Rename(
      Guid keyId,
      [FromQuery] Guid tenantId,
      [FromBody] RenameInstallerKeyRequestDto request,
      CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (!User.TryGetPrincipalId(out var callerPrincipalId))
    {
      return Unauthorized();
    }

    var isAdmin = await CanManageAllKeysAsync(resolvedTenantId, cancellationToken);
    var result = await _installerKeyManager.RenameKey(keyId, request.FriendlyName, callerPrincipalId, resolvedTenantId, isAdmin);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  private static InstallerKeyDto ToV1Dto(InternalDtos.AgentInstallerKeyDto source)
  {
    return new InstallerKeyDto(
      source.Id,
      source.CreatorId,
      source.CreatorName,
      source.KeyType,
      source.CreatedAt,
      source.AllowedUses,
      source.Expiration,
      source.FriendlyName,
      source.UsageCount);
  }

  private static InstallerKeyUsageDto ToV1Dto(InternalDtos.AgentInstallerKeyUsageDto source)
  {
    return new InstallerKeyUsageDto(
      source.Id,
      source.DeviceId,
      source.Timestamp,
      source.RemoteIpAddress);
  }

  // Collapse creator-mismatch (403) into 404 so caller-supplied tenant ids cannot act as
  // an existence oracle against other tenants' keys.
  private static IActionResult ToV1Failure<T>(HttpResult<T> result) =>
    ToV1Failure(result.ToHttpResult());

  private static IActionResult ToV1Failure(HttpResult result)
  {
    return result.ErrorCode == HttpResultErrorCode.Forbidden
      ? new NotFoundResult()
      : result.ToActionResult();
  }

  /// <summary>
  /// Server principals reach every key by definition of their cross-tenant access mode. Tenant
  /// principals need the matching others permission, evaluated against the resolved tenant.
  /// </summary>
  private async Task<bool> CanAccessAllKeysAsync(
    string permissionName,
    Guid resolvedTenantId,
    CancellationToken cancellationToken)
  {
    if (User.IsServerPrincipal())
    {
      return true;
    }

    var principal = User.ToPrincipalDescriptor();
    if (principal is null)
    {
      return false;
    }

    var result = await _permissionEvaluator.Evaluate(
      principal,
      permissionName,
      _resourceFactory.CreateTenant(resolvedTenantId),
      cancellationToken);

    return result.Allowed;
  }

  private Task<bool> CanManageAllKeysAsync(Guid resolvedTenantId, CancellationToken cancellationToken) =>
    CanAccessAllKeysAsync(PermissionNames.InstallerKeyOthersWrite, resolvedTenantId, cancellationToken);

  private Task<bool> CanReadAllKeysAsync(Guid resolvedTenantId, CancellationToken cancellationToken) =>
    CanAccessAllKeysAsync(PermissionNames.InstallerKeyOthersRead, resolvedTenantId, cancellationToken);
}

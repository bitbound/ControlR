using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Extensions.Dtos.V1;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route($"{HttpConstants.V1.TenantServiceAccountsEndpoint}/{{tenantId:guid}}")]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class TenantServiceAccountsController(
  IServiceAccountManager serviceAccountManager) : ControllerBase
{
  private readonly IServiceAccountManager _serviceAccountManager = serviceAccountManager;

  [HttpPost("{serviceAccountId:guid}/credentials")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRotateCredentials)]
  [ProducesResponseType<CreateServiceAccountCredentialResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<CreateServiceAccountCredentialResponseDto>> AddCredential(
    Guid tenantId,
    Guid serviceAccountId,
    [FromBody] CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.AddCredentialForTenant(
      serviceAccountId, resolvedTenantId, request.Name, request.ExpiresAt, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value.ToDto());
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireServiceAccountWrite)]
  [ProducesResponseType<TenantServiceAccountDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<TenantServiceAccountDto>> Create(
    Guid tenantId,
    [FromBody] CreateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.CreateForTenant(
      request.Name, request.Description, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return CreatedAtAction(
      nameof(Get),
      new { tenantId = resolvedTenantId, serviceAccountId = result.Value.Id },
      result.Value.ToTenantServiceAccountDto());
  }

  [HttpDelete("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    Guid tenantId,
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.DeleteForTenant(serviceAccountId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRead)]
  [ProducesResponseType<TenantServiceAccountDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<TenantServiceAccountDto>> Get(
    Guid tenantId,
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await _serviceAccountManager.GetForTenant(serviceAccountId, resolvedTenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value.ToTenantServiceAccountDto());
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRead)]
  [ProducesResponseType<TenantServiceAccountsResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<TenantServiceAccountsResponseDto>> GetAll(
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var accounts = await _serviceAccountManager.GetAllForTenant(resolvedTenantId, cancellationToken);
    return Ok(new TenantServiceAccountsResponseDto
    {
      Items = [.. accounts.Select(x => x.ToTenantServiceAccountDto())]
    });
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}/purge")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRotateCredentials)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> PurgeCredential(
    Guid tenantId,
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.PurgeCredentialForTenant(
      serviceAccountId, credentialId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRotateCredentials)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeCredential(
    Guid tenantId,
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.RevokeCredentialForTenant(
      serviceAccountId, credentialId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPut("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountWrite)]
  [ProducesResponseType<TenantServiceAccountDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<TenantServiceAccountDto>> Update(
    Guid tenantId,
    Guid serviceAccountId,
    [FromBody] UpdateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.UpdateForTenant(
      serviceAccountId,
      resolvedTenantId,
      request.Name,
      request.Description,
      request.IsEnabled,
      actor,
      cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value.ToTenantServiceAccountDto());
  }
}

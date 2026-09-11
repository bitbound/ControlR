using ControlR.Web.Server.Authz.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.TenantServiceAccountsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class TenantServiceAccountsController(IServiceAccountManager serviceAccountManager) : ControllerBase
{
  private readonly IServiceAccountManager _serviceAccountManager = serviceAccountManager;

  [HttpPost("{serviceAccountId:guid}/credentials")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRotateCredentials)]
  public async Task<ActionResult<InternalDtos.CreateTenantServiceAccountCredentialResponseDto>> AddCredential(
    [FromRoute] Guid serviceAccountId,
    [FromBody] InternalDtos.CreateTenantServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.AddCredentialForTenant(
      serviceAccountId, tenantId, request.Name, request.ExpiresAt, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(new InternalDtos.CreateTenantServiceAccountCredentialResponseDto(
      MapCredentialToDto(result.Value.Credential),
      result.Value.PlainTextSecretKey));
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireServiceAccountWrite)]
  public async Task<ActionResult<InternalDtos.TenantServiceAccountDto>> Create(
    [FromBody] InternalDtos.CreateTenantServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.CreateForTenant(
      request.Name, request.Description, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(MapToDto(result.Value));
  }

  [HttpDelete("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountWrite)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.DeleteForTenant(serviceAccountId, tenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRead)]
  public async Task<ActionResult<InternalDtos.TenantServiceAccountDto>> Get(
    [FromRoute] Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var result = await _serviceAccountManager.GetForTenant(serviceAccountId, tenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(MapToDto(result.Value));
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRead)]
  public async Task<ActionResult<IReadOnlyList<InternalDtos.TenantServiceAccountDto>>> GetAll(
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var accounts = await _serviceAccountManager.GetAllForTenant(tenantId, cancellationToken);
    return Ok(accounts.Select(MapToDto).ToList());
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}/purge")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRotateCredentials)]
  public async Task<IActionResult> PurgeCredential(
    [FromRoute] Guid serviceAccountId,
    [FromRoute] Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.PurgeCredentialForTenant(
      serviceAccountId, credentialId, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountRotateCredentials)]
  public async Task<IActionResult> RevokeCredential(
    [FromRoute] Guid serviceAccountId,
    [FromRoute] Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.RevokeCredentialForTenant(
      serviceAccountId, credentialId, tenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPut("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServiceAccountWrite)]
  public async Task<ActionResult<InternalDtos.TenantServiceAccountDto>> Update(
    [FromRoute] Guid serviceAccountId,
    [FromBody] InternalDtos.UpdateTenantServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.UpdateForTenant(
      serviceAccountId, tenantId, request.Name, request.Description, request.IsEnabled, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(MapToDto(result.Value));
  }

  private static InternalDtos.TenantServiceAccountCredentialDto MapCredentialToDto(ServiceAccountCredentialResult result)
  {
    return new InternalDtos.TenantServiceAccountCredentialDto(
      result.Id,
      result.Name,
      result.CreatedAt,
      result.ExpiresAt,
      result.RevokedAt,
      result.LastUsedAt);
  }

  private static InternalDtos.TenantServiceAccountDto MapToDto(ServiceAccountResult result)
  {
    return new InternalDtos.TenantServiceAccountDto(
      result.Id,
      result.Name,
      result.Description,
      result.IsEnabled,
      result.CreatedAt,
      [.. result.Credentials.Select(MapCredentialToDto)]);
  }
}

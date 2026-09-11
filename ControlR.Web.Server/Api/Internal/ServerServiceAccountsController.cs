using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.ServerServiceAccountsEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class ServerServiceAccountsController(IServiceAccountManager serviceAccountManager) : ControllerBase
{
  private readonly IServiceAccountManager _serviceAccountManager = serviceAccountManager;

  [HttpPost("{serviceAccountId:guid}/credentials")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRotateCredentials)]
  public async Task<ActionResult<InternalDtos.CreateServerServiceAccountCredentialResponseDto>> AddCredential(
    [FromRoute] Guid serviceAccountId,
    [FromBody] InternalDtos.CreateServerServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.AddCredentialForServer(serviceAccountId, request.Name, request.ExpiresAt, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(new InternalDtos.CreateServerServiceAccountCredentialResponseDto(
      MapCredentialToDto(result.Value.Credential),
      result.Value.PlainTextSecretKey));
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsWrite)]
  public async Task<ActionResult<InternalDtos.ServerServiceAccountDto>> Create(
    [FromBody] InternalDtos.CreateServerServiceAccountRequestDto request,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    CancellationToken cancellationToken)
  {
    var caller = User.ToPrincipalDescriptor();
    if (request.AccessMode == ServiceAccountAccessMode.Unrestricted)
    {
      if (caller is null)
      {
        return Unauthorized();
      }

      var decision = await permissionEvaluator.Evaluate(
        caller,
        PermissionNames.ServerPermissionsWrite,
        new ResourceDescriptor(PermissionScopeKind.Server),
        cancellationToken);

      if (!decision.Allowed)
      {
        return Forbid();
      }
    }

    var result = await _serviceAccountManager.CreateForServer(
      request.Name, request.Description, request.AccessMode, cancellationToken,
      actor: caller);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(MapToDto(result.Value));
  }

  [HttpDelete("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsWrite)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.DeleteForServer(serviceAccountId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRead)]
  public async Task<ActionResult<InternalDtos.ServerServiceAccountDto>> Get(
    [FromRoute] Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    var result = await _serviceAccountManager.GetForServer(serviceAccountId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(MapToDto(result.Value));
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRead)]
  public async Task<ActionResult<IReadOnlyList<InternalDtos.ServerServiceAccountDto>>> GetAll(
    CancellationToken cancellationToken)
  {
    var accounts = await _serviceAccountManager.GetAllForServer(cancellationToken);
    return Ok(accounts.Select(MapToDto).ToList());
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}/purge")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRotateCredentials)]
  public async Task<IActionResult> PurgeCredential(
    [FromRoute] Guid serviceAccountId,
    [FromRoute] Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.PurgeCredentialForServer(serviceAccountId, credentialId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRotateCredentials)]
  public async Task<IActionResult> RevokeCredential(
    [FromRoute] Guid serviceAccountId,
    [FromRoute] Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.RevokeCredentialForServer(serviceAccountId, credentialId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpPut("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsWrite)]
  public async Task<ActionResult<InternalDtos.ServerServiceAccountDto>> Update(
    [FromRoute] Guid serviceAccountId,
    [FromBody] InternalDtos.UpdateServerServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await _serviceAccountManager.UpdateForServer(
      serviceAccountId, request.Name, request.Description, request.IsEnabled, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(MapToDto(result.Value));
  }

  private static InternalDtos.ServerServiceAccountCredentialDto MapCredentialToDto(ServiceAccountCredentialResult result)
  {
    return new InternalDtos.ServerServiceAccountCredentialDto(
      result.Id,
      result.Name,
      result.CreatedAt,
      result.ExpiresAt,
      result.RevokedAt,
      result.LastUsedAt);
  }

  private static InternalDtos.ServerServiceAccountDto MapToDto(ServiceAccountResult result)
  {
    return new InternalDtos.ServerServiceAccountDto(
      result.Id,
      result.Name,
      result.Description,
      result.IsEnabled,
      result.AccessMode,
      result.CreatedAt,
      [.. result.Credentials.Select(MapCredentialToDto)]);
  }
}

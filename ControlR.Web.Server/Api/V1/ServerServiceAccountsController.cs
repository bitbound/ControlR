using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServiceAccounts;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Extensions.Dtos.V1;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V1;

[Route(HttpConstants.V1.ServerServiceAccountsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class ServerServiceAccountsController(
  IServiceAccountManager serviceAccountManager) : ControllerBase
{
  private readonly IServiceAccountManager _serviceAccountManager = serviceAccountManager;

  [HttpPost("{serviceAccountId:guid}/credentials")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRotateCredentials)]
  [ProducesResponseType<CreateServiceAccountCredentialResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<CreateServiceAccountCredentialResponseDto>> AddCredential(
    Guid serviceAccountId,
    [FromBody] CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.AddCredentialForServer(serviceAccountId, request.Name, request.ExpiresAt, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value.ToDto());
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsWrite)]
  [ProducesResponseType<ServerServiceAccountDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<ServerServiceAccountDto>> Create(
    [FromBody] CreateServerServiceAccountRequestDto request,
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

    return CreatedAtAction(nameof(Get), new { serviceAccountId = result.Value.Id }, result.Value.ToServerServiceAccountDto());
  }

  [HttpDelete("{serviceAccountId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
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
  [ProducesResponseType<ServerServiceAccountDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ServerServiceAccountDto>> Get(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    var result = await _serviceAccountManager.GetForServer(serviceAccountId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value.ToServerServiceAccountDto());
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRead)]
  [ProducesResponseType<ServerServiceAccountsResponseDto>(StatusCodes.Status200OK)]
  public async Task<ActionResult<ServerServiceAccountsResponseDto>> GetAll(CancellationToken cancellationToken)
  {
    var accounts = await _serviceAccountManager.GetAllForServer(cancellationToken);
    return Ok(new ServerServiceAccountsResponseDto
    {
      Items = [.. accounts.Select(x => x.ToServerServiceAccountDto())]
    });
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}")]
  [Authorize(Policy = PolicyNames.RequireServerServiceAccountsRotateCredentials)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
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
  [ProducesResponseType<ServerServiceAccountDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ServerServiceAccountDto>> Update(
    Guid serviceAccountId,
    [FromBody] UpdateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.UpdateForServer(
      serviceAccountId, request.Name, request.Description, request.IsEnabled, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToHttpResult().ToActionResult();
    }

    return Ok(result.Value.ToServerServiceAccountDto());
  }
}

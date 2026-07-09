using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.ServiceAccountsEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(OpenApiConstants.CurrentApiVersion)]
public class ServiceAccountsController(
  IServiceAccountManager serviceAccountManager) : ControllerBase
{
  private readonly IServiceAccountManager _serviceAccountManager = serviceAccountManager;

  [HttpPost("{serviceAccountId:guid}/credentials")]
  public async Task<ActionResult<CreateServiceAccountCredentialResponseDto>> AddCredential(
    Guid serviceAccountId,
    [FromBody] CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.IsServerPrincipal())
    {
      return Forbid();
    }

    var result = await _serviceAccountManager.AddCredential(serviceAccountId, request.Name, cancellationToken);
    if (!result.IsSuccess)
    {
      if (string.Equals(result.Reason, "Server service account not found.", StringComparison.Ordinal))
      {
        return NotFound(result.Reason);
      }

      if (string.Equals(result.Reason, "Service account is disabled.", StringComparison.Ordinal))
      {
        return Conflict(result.Reason);
      }

      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpPost]
  public async Task<ActionResult<CreateServiceAccountResponseDto>> Create(
    [FromBody] CreateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.IsServerPrincipal())
    {
      return Forbid();
    }

    var result = await _serviceAccountManager.CreateServer(request.Name, request.Description, cancellationToken);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return CreatedAtAction(nameof(GetAll), new { }, result.Value);
  }

  [HttpDelete("{serviceAccountId:guid}")]
  public async Task<IActionResult> Delete(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    if (!User.IsServerPrincipal())
    {
      return Forbid();
    }

    var result = await _serviceAccountManager.Delete(serviceAccountId, cancellationToken);
    if (!result.IsSuccess)
    {
      return NotFound(result.Reason);
    }

    return NoContent();
  }

  [HttpGet]
  public async Task<ActionResult<List<ServiceAccountDto>>> GetAll(CancellationToken cancellationToken)
  {
    if (!User.IsServerPrincipal())
    {
      return Forbid();
    }

    var accounts = await _serviceAccountManager.GetAllServer(cancellationToken);
    return Ok(accounts);
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}")]
  public async Task<IActionResult> RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    if (!User.IsServerPrincipal())
    {
      return Forbid();
    }

    var result = await _serviceAccountManager.RevokeCredential(serviceAccountId, credentialId, cancellationToken);
    if (!result.IsSuccess)
    {
      return NotFound(result.Reason);
    }

    return NoContent();
  }
}
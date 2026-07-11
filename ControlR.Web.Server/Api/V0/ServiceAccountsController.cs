using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;
using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Extensions;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.ServiceAccountsEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V0)]
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
      return result.ToActionResult();
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

    var result = await _serviceAccountManager.CreateForServer(request.Name, request.Description, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
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
      return result.ToActionResult();
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
      return result.ToActionResult();
    }

    return NoContent();
  }
}
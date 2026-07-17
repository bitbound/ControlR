using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;
using ControlR.Web.Server.Authn;
using Microsoft.AspNetCore.Mvc;

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
  [ProducesResponseType<CreateServiceAccountCredentialResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<CreateServiceAccountCredentialResponseDto>> AddCredential(
    Guid serviceAccountId,
    [FromBody] CreateServiceAccountCredentialRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await _serviceAccountManager.AddCredential(serviceAccountId, request.Name, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpPost]
  [ProducesResponseType<CreateServiceAccountResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<CreateServiceAccountResponseDto>> Create(
    [FromBody] CreateServiceAccountRequestDto request,
    CancellationToken cancellationToken)
  {
    var result = await _serviceAccountManager.CreateForServer(request.Name, request.Description, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return CreatedAtAction(nameof(Get), new { serviceAccountId = result.Value.ServiceAccount.Id }, result.Value);
  }

  [HttpDelete("{serviceAccountId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    var principalClaim = User.FindFirst(PrincipalClaimTypes.PrincipalId);
    if (principalClaim is null)
    {
      return Unauthorized();
    }

    if (!Guid.TryParse(principalClaim.Value, out var principalId))
    {
      return Unauthorized();
    }

    var result = await _serviceAccountManager.Delete(serviceAccountId, principalId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }

  [HttpGet("{serviceAccountId:guid}")]
  [ProducesResponseType<ServiceAccountDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ServiceAccountDto>> Get(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    var result = await _serviceAccountManager.Get(serviceAccountId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return Ok(result.Value);
  }

  [HttpGet]
  public async Task<ActionResult<List<ServiceAccountDto>>> GetAll(CancellationToken cancellationToken)
  {
    var accounts = await _serviceAccountManager.GetAllForServer(cancellationToken);
    return Ok(accounts);
  }

  [HttpDelete("{serviceAccountId:guid}/credentials/{credentialId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    var result = await _serviceAccountManager.RevokeCredential(serviceAccountId, credentialId, cancellationToken);
    if (!result.IsSuccess)
    {
      return result.ToActionResult();
    }

    return NoContent();
  }
}

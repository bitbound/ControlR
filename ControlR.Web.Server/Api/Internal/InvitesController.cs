using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.InvitesEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class InvitesController : ControllerBase
{
  [HttpPost("accept")]
  [AllowAnonymous]
  public async Task<ActionResult<InternalDtos.AcceptInvitationResponseDto>> AcceptInvite(
    [FromBody] InternalDtos.AcceptInvitationRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    var result = await tenantInvitesProvider.AcceptInvite(dto);

    if (!result.IsSuccess)
    {
      return new InternalDtos.AcceptInvitationResponseDto(false, result.Reason);
    }

    return result.Value;
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  public async Task<ActionResult<InternalDtos.TenantInviteResponseDto>> Create(
    [FromBody] InternalDtos.TenantInviteRequestDto dto,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var origin = Request.ToOrigin();
    var result = await tenantInvitesProvider.CreateInvite(
      dto.InviteeEmail,
      tenantId,
      origin,
      HttpContext.RequestAborted);

    return result.ToActionResult();
  }

  [HttpDelete("{inviteId:guid}")]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid inviteId,
    [FromServices] ITenantInvitesProvider tenantInvitesProvider)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var result = await tenantInvitesProvider.DeleteInvite(inviteId, tenantId);

    return result.ToActionResult();
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireUsersRead)]
  public async Task<ActionResult<InternalDtos.TenantInviteResponseDto[]>> GetAll(
    [FromServices] ITenantInvitesProvider tenantInvitesProvider,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromServices] IResourceDescriptorFactory resourceFactory)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    // Only invite managers (TenantUsersWrite) may see the activation code; read-only users
    // receive the invite metadata without the bearer secret. Evaluate the current credential
    // directly so a narrowed PAT cannot inherit the owning user's write permission.
    var callerPrincipal = User.ToPrincipalDescriptor();
    if (callerPrincipal is null)
    {
      return Unauthorized();
    }

    var resource = resourceFactory.CreateTenant(tenantId);
    var evalResult = await permissionEvaluator.Evaluate(
      callerPrincipal, PermissionNames.TenantUsersWrite, resource, HttpContext.RequestAborted);

    var origin = Request.ToOrigin();
    return await tenantInvitesProvider.GetAllInvites(tenantId, origin, evalResult.Allowed);
  }
}

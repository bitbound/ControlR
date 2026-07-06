using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Client.Authz.Policies;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.UsersEndpoint + "/server")]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
public class ServerUsersController : ControllerBase
{
  [HttpPost("{userId:guid}/reset-password")]
  public async Task<ActionResult<AdminResetPasswordResponseDto>> AdminResetPassword(
    [FromRoute] Guid userId,
    [FromBody] ServerAdminResetPasswordRequestDto request,
    [FromServices] IPasswordManager passwordManager)
  {
    var result = await passwordManager.AdminResetPassword(request.TenantId, userId);
    if (!result.IsSuccess)
    {
      if (string.Equals(result.Reason, "User not found.", StringComparison.Ordinal))
      {
        return NotFound();
      }

      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpPost]
  public async Task<ActionResult<UserResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IUserCreator userCreator,
    [FromBody] ServerCreateUserRequestDto request)
  {
    var requestRoleIds = request.RoleIds?.ToArray();

    if (requestRoleIds is { Length: > 0 } roleIds)
    {
      var roles = await appDb.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync();
      var foundRoleIds = roles.Select(r => r.Id).ToHashSet();
      var missingRoleIds = roleIds.Except(foundRoleIds).ToList();
      if (missingRoleIds.Count != 0)
      {
        return BadRequest($"Roles not found: {string.Join(',', missingRoleIds)}");
      }

      if (roles.Any(r => r.Name == RoleNames.ServerAdministrator))
      {
        return BadRequest("Server service accounts cannot assign the server administrator role.");
      }
    }

    var createResult = await userCreator.CreateUser(
      string.IsNullOrWhiteSpace(request.Email) ? request.UserName : request.Email,
      request.Password ?? string.Empty,
      request.TenantId,
      requestRoleIds,
      request.TagIds,
      cancellationToken: HttpContext.RequestAborted);

    if (!createResult.Succeeded)
    {
      return BadRequest(createResult.IdentityResult.Errors.Select(e => e.Description));
    }

    var user = createResult.User;
    if (user is null)
    {
      return BadRequest("User creation failed");
    }

    var response = new UserResponseDto(user.Id, user.UserName, user.Email);
    return CreatedAtAction(nameof(Create), new { id = user.Id }, response);
  }
}
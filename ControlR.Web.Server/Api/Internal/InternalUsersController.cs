using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Services.Users;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Authz.Policies;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UsersEndpoint)]
[ApiController]
[Authorize(Policy = RequireUserPrincipalPolicy.PolicyName)]
public class InternalUsersController : ControllerBase
{
  [HttpPost("{userId:guid}/reset-password")]
  public async Task<ActionResult<AdminResetPasswordResponseDto>> AdminResetPassword(
    [FromRoute] Guid userId,
    [FromServices] IPasswordManager passwordManager)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var result = await passwordManager.AdminResetPassword(tenantId, userId);
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
    [FromBody] CreateUserRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

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
        if (!User.TryGetUserId(out var callerUserId))
        {
          return BadRequest("Caller user id not found");
        }

        var callerUser = await appDb.Users.FirstOrDefaultAsync(u => u.Id == callerUserId);
        if (callerUser == null)
        {
          return BadRequest("Caller user not found");
        }

        if (!await userManager.IsInRoleAsync(callerUser, RoleNames.ServerAdministrator))
        {
          return Forbid();
        }
      }
    }

    var createResult = await userCreator.CreateUser(
      string.IsNullOrWhiteSpace(request.Email) ? request.UserName : request.Email,
      request.Password ?? string.Empty,
      tenantId,
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
    return CreatedAtAction(nameof(GetAll), new { id = user.Id }, response);
  }

  [HttpPost("{userId:guid}/personal-access-tokens")]
  public async Task<ActionResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromBody] CreatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (!targetExists)
    {
      return NotFound();
    }

    var result = await personalAccessTokenManager.CreateToken(request, userId);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [HttpDelete("{userId:guid}")]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid userId,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var user = await appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (user == null)
    {
      return NotFound();
    }

    var result = await userManager.DeleteAsync(user);
    if (!result.Succeeded)
    {
      return BadRequest(result.Errors.Select(e => e.Description));
    }

    return NoContent();
  }

  [HttpDelete("{userId:guid}/personal-access-tokens/{tokenId:guid}")]
  public async Task<IActionResult> DeleteUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (!targetExists)
    {
      return NotFound();
    }

    var result = await personalAccessTokenManager.Delete(tokenId, userId);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return NoContent();
  }

  [HttpGet]
  public async Task<ActionResult<List<UserResponseDto>>> GetAll(
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    return await appDb.Users
      .Where(x => x.TenantId == tenantId)
      .Select(x => new UserResponseDto(x.Id, x.UserName, x.Email))
      .ToListAsync();
  }

  [HttpGet("{userId:guid}/personal-access-tokens")]
  public async Task<ActionResult<IEnumerable<PersonalAccessTokenDto>>> GetUserPersonalAccessTokens(
    [FromRoute] Guid userId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (!targetExists)
    {
      return NotFound();
    }

    var tokens = await personalAccessTokenManager.GetForUser(userId);
    return Ok(tokens);
  }

  [HttpPut("{userId:guid}/personal-access-tokens/{tokenId:guid}")]
  public async Task<ActionResult<PersonalAccessTokenDto>> UpdateUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromBody] UpdatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == tenantId);

    if (!targetExists)
    {
      return NotFound();
    }

    var result = await personalAccessTokenManager.Update(tokenId, request, userId);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }
}

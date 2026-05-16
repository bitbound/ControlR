using ControlR.Libraries.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Services.Users;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
public class UsersController : ControllerBase
{
  [HttpPost("{id:guid}/admin-reset-password")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<AdminResetPasswordResponseDto>> AdminResetPassword(
    [FromRoute] Guid id,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
    }

    var targetUser = await appDb.Users
      .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

    if (targetUser is null)
    {
      return NotFound();
    }

    var tempPassword = RandomGenerator.CreateAccessToken()[..16];
    var resetCode = await userManager.GeneratePasswordResetTokenAsync(targetUser);
    var resetResult = await userManager.ResetPasswordAsync(targetUser, resetCode, tempPassword);
    if (!resetResult.Succeeded)
    {
      return BadRequest(resetResult.Errors.Select(e => e.Description));
    }

    targetUser.RequirePasswordChange = true;
    await userManager.UpdateAsync(targetUser);

    return Ok(new AdminResetPasswordResponseDto(tempPassword));
  }

  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<UserResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] IUserCreator userCreator,
    [FromBody] CreateUserRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
    }

    // If roles include ServerAdministrator, ensure caller is server admin
    if (request.RoleIds?.Any() == true)
    {
      var roles = await appDb.Roles.Where(r => request.RoleIds.Contains(r.Id)).ToListAsync();
      var foundRoleIds = roles.Select(r => r.Id).ToHashSet();
      var missingRoleIds = request.RoleIds.Except(foundRoleIds).ToList();
      if (missingRoleIds.Count != 0)
      {
        return BadRequest($"Roles not found: {string.Join(',', missingRoleIds)}");
      }

      if (roles.Any(r => r.Name == RoleNames.ServerAdministrator))
      {
        // Resolve caller's user and check role via UserManager to avoid relying on ClaimsPrincipal state
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

    // Delegate creation (including optional role/tag assignment) to IUserCreator
    var createResult = await userCreator.CreateUser(
      string.IsNullOrWhiteSpace(request.Email) ? request.UserName : request.Email,
      request.Password ?? string.Empty,
      tenantId,
      request.RoleIds,
      request.TagIds);

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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromBody] CreatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
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

  [HttpDelete("{id:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid id,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb)
  {
    var user = await appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == id);

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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<IActionResult> DeleteUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<List<UserResponseDto>>> GetAll(
    [FromServices] AppDb appDb)
  {
    return await appDb.Users
      .Select(x => new UserResponseDto(x.Id, x.UserName, x.Email))
      .ToListAsync();
  }

  [HttpGet("{userId:guid}/personal-access-tokens")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<IEnumerable<PersonalAccessTokenDto>>> GetUserPersonalAccessTokens(
    [FromRoute] Guid userId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
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
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<PersonalAccessTokenDto>> UpdateUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromBody] UpdatePersonalAccessTokenRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found");
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

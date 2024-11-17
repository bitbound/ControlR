using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client;
using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = RoleNames.TenantAdministrator)]
public class InvitesController : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<TenantInviteResponseDto[]>> GetAll(
    [FromServices]AppDb appDb)
  {
    var origin = Request.ToOrigin();
    return await appDb.TenantInvites
      .Select(x => new TenantInviteResponseDto(
        x.Id,
        x.CreatedAt,
        x.InviteeEmail,
        new Uri(origin, $"{ClientRoutes.InviteConfirmationBase}/{x.ActivationCode}")))
      .ToArrayAsync();
  }

  [HttpPost]
  public async Task<ActionResult<TenantInviteResponseDto>> Create(
    [FromBody]TenantInviteRequestDto dto,
    [FromServices]AppDb appDb,
    [FromServices]IUserCreator userCreator)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return Unauthorized();
    }

    var normalizedEmail = dto.InviteeEmail.Trim().ToLower();

    if (await appDb.TenantInvites.AnyAsync(x => x.InviteeEmail == normalizedEmail))
    {
      return Conflict("Invitee already has a pending invite.");
    }

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
    if (await appDb.Users.AnyAsync(x => x.Email!.ToLower() == normalizedEmail))
    {
      return Conflict("User already exists in the database.");
    }
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

    var randomPassword = RandomGenerator.GenerateString(64);
    var createResult = await userCreator.CreateUser(
      dto.InviteeEmail, 
      password: randomPassword, 
      tenantId: tenantId);

    if (!createResult.Succeeded)
    {
      var firstError = createResult.IdentityResult.Errors.FirstOrDefault();
      
      if (firstError is { Code: nameof(IdentityErrorDescriber.DuplicateUserName) } idError)
      {
        return Conflict("User already exists.");
      }

      return Problem("Failed to create user.");
    }

    var invite = new TenantInvite()
    {
      ActivationCode = RandomGenerator.GenerateString(32),
      InviteeEmail = normalizedEmail,
      TenantId = tenantId,
    };
    await appDb.TenantInvites.AddAsync(invite);
    await appDb.SaveChangesAsync();
    var newUser = createResult.User;
    var origin = Request.ToOrigin();
    var inviteUrl = new Uri(origin, $"{ClientRoutes.InviteConfirmationBase}/{invite.ActivationCode}");
    var retDto = new TenantInviteResponseDto(invite.Id, invite.CreatedAt, normalizedEmail, inviteUrl);
    return Ok(retDto);
  }

  [HttpDelete("{inviteId:guid}")]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid inviteId,
    [FromServices] AppDb appDb,
    [FromServices]UserManager<AppUser> userManager)
  {
    var invite = await appDb.TenantInvites.FindAsync(inviteId);
    if (invite is null)
    {
      return NotFound();
    }

    var user = await userManager.FindByEmailAsync(invite.InviteeEmail);
    appDb.TenantInvites.Remove(invite);
    await appDb.SaveChangesAsync();

    if (user is not null)
    {
      await userManager.DeleteAsync(user);
    }
    return NoContent();
  }
}

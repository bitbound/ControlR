using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Users;

namespace ControlR.Web.Server.Services;

public interface ITenantInvitesProvider
{
  Task<HttpResult<AcceptInvitationResponseDto>> AcceptInvite(
    AcceptInvitationRequestDto dto);

  Task<HttpResult<TenantInviteResponseDto>> CreateInvite(
    string inviteeEmail,
    Guid tenantId,
    Uri origin,
    CancellationToken cancellationToken = default);

  Task<HttpResult> DeleteInvite(
    Guid inviteId,
    Guid tenantId);

  Task<TenantInviteResponseDto[]> GetAllInvites(
    Guid tenantId,
    Uri origin);
}

public class TenantInvitesProvider(
  IDbContextFactory<AppDb> dbContextFactory,
  UserManager<AppUser> userManager,
  IUserCreator userCreator,
  ILogger<TenantInvitesProvider> logger) : ITenantInvitesProvider
{
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<TenantInvitesProvider> _logger = logger;
  private readonly IUserCreator _userCreator = userCreator;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<HttpResult<AcceptInvitationResponseDto>> AcceptInvite(
    AcceptInvitationRequestDto dto)
  {
    _logger.LogInformation("Accepting invitation for email: {Email}", dto.Email);

    await using var appDb = await _dbContextFactory.CreateDbContextAsync();

    var normalizedEmail = dto.Email.Trim().ToLower();

    var invite = await appDb.TenantInvites
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(x =>
        x.ActivationCode == dto.ActivationCode &&
        x.InviteeEmail == normalizedEmail);

    if (invite is null)
    {
      _logger.LogWarning(
        "Invitation not found for activation code: {ActivationCode} and email: {Email}",
        dto.ActivationCode,
        normalizedEmail);
      return HttpResult.Fail<AcceptInvitationResponseDto>(HttpResultErrorCode.NotFound, "Invitation not found.");
    }

    var invitee = await _userManager.FindByEmailAsync(dto.Email);
    if (invitee is null)
    {
      _logger.LogWarning("Invitee user account not found for email: {Email}", dto.Email);
      return HttpResult.Fail<AcceptInvitationResponseDto>(HttpResultErrorCode.NotFound, "Invitee user account not found.");
    }

    var resetCode = await _userManager.GeneratePasswordResetTokenAsync(invitee);
    var idResult = await _userManager.ResetPasswordAsync(invitee, resetCode, dto.Password);
    if (!idResult.Succeeded)
    {
      foreach (var error in idResult.Errors)
      {
        _logger.LogWarning("Password reset error: {Code} - {Description}", error.Code, error.Description);
      }
      return HttpResult.Fail<AcceptInvitationResponseDto>(HttpResultErrorCode.BadRequest, "Failed to set new password");
    }

    // Clear only UserRoles and Tags when moving to new tenant
    var trackedUser = await ClearUserRolesAndTags(appDb, invitee.Id);

    // Update tenant ID on the tracked entity
    trackedUser.TenantId = invite.TenantId;
    appDb.TenantInvites.Remove(invite);
    await appDb.SaveChangesAsync();

    var response = new AcceptInvitationResponseDto(true);
    return HttpResult.Ok(response);
  }

  public async Task<HttpResult<TenantInviteResponseDto>> CreateInvite(
    string inviteeEmail,
    Guid tenantId,
    Uri origin,
    CancellationToken cancellationToken = default)
  {
    var normalizedEmail = inviteeEmail.Trim().ToLower();

    await using var appDb = await _dbContextFactory.CreateDbContextAsync();

    if (await appDb.TenantInvites.AnyAsync(x => x.InviteeEmail == normalizedEmail))
    {
      return HttpResult.Fail<TenantInviteResponseDto>(HttpResultErrorCode.Conflict, "Invitee already has a pending invite.");
    }

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
    if (await appDb.Users.AnyAsync(x => x.Email!.ToLower() == normalizedEmail))
    {
      return HttpResult.Fail<TenantInviteResponseDto>(HttpResultErrorCode.Conflict, "User already exists in the database.");
    }
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

    var randomPassword = RandomGenerator.GenerateString(64);
    var createResult = await _userCreator.CreateUser(
      inviteeEmail,
      password: randomPassword,
      tenantId: tenantId,
      cancellationToken: cancellationToken);

    if (!createResult.Succeeded)
    {
      var firstError = createResult.IdentityResult.Errors.FirstOrDefault();

      if (firstError is { Code: nameof(IdentityErrorDescriber.DuplicateUserName) })
      {
        return HttpResult.Fail<TenantInviteResponseDto>(HttpResultErrorCode.Conflict, "User already exists.");
      }

      return HttpResult.Fail<TenantInviteResponseDto>(HttpResultErrorCode.InternalServerError, "Failed to create user.");
    }

    var invite = new TenantInvite()
    {
      ActivationCode = RandomGenerator.GenerateString(64),
      InviteeEmail = normalizedEmail,
      TenantId = tenantId,
    };
    await appDb.TenantInvites.AddAsync(invite);
    await appDb.SaveChangesAsync();

    var inviteUrl = new Uri(origin, $"{ClientRoutes.InviteConfirmationBase}/{invite.ActivationCode}");
    var retDto = new TenantInviteResponseDto(invite.Id, invite.CreatedAt, normalizedEmail, inviteUrl);
    return HttpResult.Ok(retDto);
  }

  public async Task<HttpResult> DeleteInvite(Guid inviteId, Guid tenantId)
  {
    await using var appDb = await _dbContextFactory.CreateDbContextAsync();

    var invite = await appDb.TenantInvites.FindAsync(inviteId);
    if (invite is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Invitation not found.");
    }

    if (invite.TenantId != tenantId)
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden, "Invitation does not belong to the specified tenant.");
    }

    var user = await _userManager.FindByEmailAsync(invite.InviteeEmail);
    appDb.TenantInvites.Remove(invite);
    await appDb.SaveChangesAsync();

    if (user is not null)
    {
      await _userManager.DeleteAsync(user);
    }

    return HttpResult.Ok();
  }

  public async Task<TenantInviteResponseDto[]> GetAllInvites(Guid tenantId, Uri origin)
  {
    await using var appDb = await _dbContextFactory.CreateDbContextAsync();

    return await appDb.TenantInvites
      .Where(x => x.TenantId == tenantId)
      .Select(x => new TenantInviteResponseDto(
        x.Id,
        x.CreatedAt,
        x.InviteeEmail,
        new Uri(origin, $"{ClientRoutes.InviteConfirmationBase}/{x.ActivationCode}")))
      .ToArrayAsync();
  }

  private async Task<AppUser> ClearUserRolesAndTags(AppDb appDb, Guid userId)
  {
    // Remove UserRoles
    var userRoles = await appDb.UserRoles
      .Where(ur => ur.UserId == userId)
      .ToListAsync();

    appDb.UserRoles.RemoveRange(userRoles);

    // Remove Tags
    var user = await appDb.Users
      .IgnoreQueryFilters()
      .Include(u => u.Tags)
      .FirstOrDefaultAsync(u => u.Id == userId);

    if (user is null)
    {
      throw new InvalidOperationException($"User with ID {userId} not found.");
    }

    if (user.Tags is not null)
    {
      user.Tags.Clear();
    }

    return user;
  }
}

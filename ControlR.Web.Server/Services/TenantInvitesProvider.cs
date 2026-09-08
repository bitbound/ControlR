using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Client;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;

namespace ControlR.Web.Server.Services;

public interface ITenantInvitesProvider
{
  Task<HttpResult<InternalDtos.AcceptInvitationResponseDto>> AcceptInvite(
    InternalDtos.AcceptInvitationRequestDto dto);

  Task<HttpResult<InternalDtos.TenantInviteResponseDto>> CreateInvite(
    string inviteeEmail,
    Guid tenantId,
    Uri origin,
    CancellationToken cancellationToken = default);

  Task<HttpResult> DeleteInvite(
    Guid inviteId,
    Guid tenantId);

  Task<InternalDtos.TenantInviteResponseDto[]> GetAllInvites(
    Guid tenantId,
    Uri origin,
    bool includeActivationCode);
}

public class TenantInvitesProvider(
  IDbContextFactory<AppDb> dbContextFactory,
  UserManager<AppUser> userManager,
  IUserCreator userCreator,
  IAuthorizationChangeLogFactory changeLogFactory,
  IPermissionAssignmentSeeder assignmentSeeder,
  ILogger<TenantInvitesProvider> logger) : ITenantInvitesProvider
{
  private readonly IPermissionAssignmentSeeder _assignmentSeeder = assignmentSeeder;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<TenantInvitesProvider> _logger = logger;
  private readonly IUserCreator _userCreator = userCreator;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<HttpResult<InternalDtos.AcceptInvitationResponseDto>> AcceptInvite(
    InternalDtos.AcceptInvitationRequestDto dto)
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
      return HttpResult.Fail<InternalDtos.AcceptInvitationResponseDto>(HttpResultErrorCode.NotFound, "Invitation not found.");
    }

    var invitee = await _userManager.FindByEmailAsync(dto.Email);
    if (invitee is null)
    {
      _logger.LogWarning("Invitee user account not found for email: {Email}", dto.Email);
      return HttpResult.Fail<InternalDtos.AcceptInvitationResponseDto>(HttpResultErrorCode.NotFound, "Invitee user account not found.");
    }

    var resetCode = await _userManager.GeneratePasswordResetTokenAsync(invitee);
    var idResult = await _userManager.ResetPasswordAsync(invitee, resetCode, dto.Password);
    if (!idResult.Succeeded)
    {
      foreach (var error in idResult.Errors)
      {
        _logger.LogWarning("Password reset error: {Code} - {Description}", error.Code, error.Description);
      }
      return HttpResult.Fail<InternalDtos.AcceptInvitationResponseDto>(HttpResultErrorCode.BadRequest, "Failed to set new password");
    }

    var trackedUser = await GetTrackedUser(appDb, invitee.Id);

    trackedUser.TenantId = invite.TenantId;

    // Tenant move invalidates former-tenant grants; the rule resolver keeps stale rows inert,
    // so remove them only to clear residue and record the removals in the change log.
    var staleAssignments = await appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.User && x.PrincipalId == invitee.Id)
      .ToListAsync();

    foreach (var assignment in staleAssignments)
    {
      appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.PermissionAssignmentDeleted,
        actor: null,
        AuthorizationChangeLogTargetTypes.PermissionAssignment,
        assignment.Id,
        assignment.OwningTenantId,
        before: new PermissionAssignmentSnapshot(
          assignment.PermissionName, assignment.Effect, assignment.ScopeKind, assignment.ScopeId)));
    }

    appDb.PermissionAssignments.RemoveRange(staleAssignments);

    var staleMemberships = await appDb.UserGroupMembers
      .IgnoreQueryFilters()
      .Where(x => x.UserId == invitee.Id)
      .ToListAsync();
    appDb.UserGroupMembers.RemoveRange(staleMemberships);

    // Remove PAT scope rows keyed to the token principal so none survive the move.
    var stalePatTokenIds = appDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .Where(x => x.UserId == invitee.Id)
      .Select(x => x.Id);

    var stalePatScopeRows = await appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                  stalePatTokenIds.Contains(x.PrincipalId))
      .ToListAsync();

    foreach (var assignment in stalePatScopeRows)
    {
      appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
        AuthorizationChangeLogActions.PermissionAssignmentDeleted,
        actor: null,
        AuthorizationChangeLogTargetTypes.PermissionAssignment,
        assignment.Id,
        assignment.OwningTenantId,
        before: new PermissionAssignmentSnapshot(
          assignment.PermissionName, assignment.Effect, assignment.ScopeKind, assignment.ScopeId)));
    }

    appDb.PermissionAssignments.RemoveRange(stalePatScopeRows);

    appDb.TenantInvites.Remove(invite);
    await appDb.SaveChangesAsync();

    // The move wiped every prior grant, including the self-service baseline the account was
    // created with. Re-seed it against the destination tenant so an invited user can still
    // manage their own personal access tokens, matching a directly-created user.
    await _assignmentSeeder.SeedUserBaseline(invitee.Id, invite.TenantId);

    _logger.LogInformation(
      "User {UserId} moved to tenant {TenantId}: removed {AssignmentCount} assignment(s), {PatScopeCount} PAT scope row(s), and {MembershipCount} group membership(s).",
      invitee.Id, invite.TenantId, staleAssignments.Count, stalePatScopeRows.Count, staleMemberships.Count);

    var response = new InternalDtos.AcceptInvitationResponseDto(true);
    return HttpResult.Ok(response);
  }

  public async Task<HttpResult<InternalDtos.TenantInviteResponseDto>> CreateInvite(
    string inviteeEmail,
    Guid tenantId,
    Uri origin,
    CancellationToken cancellationToken = default)
  {
    var normalizedEmail = inviteeEmail.Trim().ToLower();

    await using var appDb = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    if (await appDb.TenantInvites.AnyAsync(x => x.InviteeEmail == normalizedEmail, cancellationToken: cancellationToken))
    {
      return HttpResult.Fail<InternalDtos.TenantInviteResponseDto>(HttpResultErrorCode.Conflict, "Invitee already has a pending invite.");
    }

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons. 
    // Reason: StringComparison is not available in EF Core LINQ-to-Entities queries.
    if (await appDb.Users.AnyAsync(x => x.Email!.ToLower() == normalizedEmail, cancellationToken: cancellationToken))
    {
      return HttpResult.Fail<InternalDtos.TenantInviteResponseDto>(HttpResultErrorCode.Conflict, "User already exists in the database.");
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
        return HttpResult.Fail<InternalDtos.TenantInviteResponseDto>(HttpResultErrorCode.Conflict, "User already exists.");
      }

      return HttpResult.Fail<InternalDtos.TenantInviteResponseDto>(HttpResultErrorCode.InternalServerError, "Failed to create user.");
    }

    var invite = new TenantInvite()
    {
      ActivationCode = RandomGenerator.GenerateString(64),
      InviteeEmail = normalizedEmail,
      TenantId = tenantId,
    };
    await appDb.TenantInvites.AddAsync(invite, cancellationToken);
    await appDb.SaveChangesAsync(cancellationToken);

    var inviteUrl = new Uri(origin, $"{ClientRoutes.InviteConfirmationBase}/{invite.ActivationCode}");
    var retDto = new InternalDtos.TenantInviteResponseDto(invite.Id, invite.CreatedAt, normalizedEmail, inviteUrl);
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

  public async Task<InternalDtos.TenantInviteResponseDto[]> GetAllInvites(
    Guid tenantId,
    Uri origin,
    bool includeActivationCode)
  {
    await using var appDb = await _dbContextFactory.CreateDbContextAsync();

    return await appDb.TenantInvites
      .Where(x => x.TenantId == tenantId)
      .Select(x => new InternalDtos.TenantInviteResponseDto(
        x.Id,
        x.CreatedAt,
        x.InviteeEmail,
        includeActivationCode
          ? new Uri(origin, $"{ClientRoutes.InviteConfirmationBase}/{x.ActivationCode}")
          : new Uri(origin, ClientRoutes.InviteConfirmationBase)))
      .ToArrayAsync();
  }

  private async Task<AppUser> GetTrackedUser(AppDb appDb, Guid userId)
  {
    var user = await appDb.Users
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(u => u.Id == userId);

    if (user is null)
    {
      throw new InvalidOperationException($"User with ID {userId} not found.");
    }

    return user;
  }
}

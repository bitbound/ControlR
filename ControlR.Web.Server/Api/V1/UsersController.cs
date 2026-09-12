using Asp.Versioning;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// User management plus the per-user personal-access-token sub-resource. Tenant scoping is
/// enforced by the required tenantId query parameter. The caller's tenant claim must match it
/// (or the caller must be a server principal). Reads keyed by the caller-resolved tenant carry
/// an explicit TenantId predicate so the boundary survives the unfiltered AppDb context a server
/// principal runs against. The lookups that run after a create address the user by its globally
/// unique id, where the id itself is the constraint. Preset assignment keeps its authority gates. Granting the
/// ServerAdministrator preset requires ServerPermissionsWrite, presets that seed tenant-scope
/// grants require ServerPermissionsWrite or TenantPermissionsWrite, and TenantAdministrator
/// additionally requires TenantPermissionsDeny unless the caller has server writes.
/// </summary>
[Route(HttpConstants.V1.UsersEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class UsersController : ControllerBase
{
  [HttpPost("{userId:guid}/reset-password")]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  [ProducesResponseType<AdminResetPasswordResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<AdminResetPasswordResponseDto>> AdminResetPassword(
    [FromServices] IPasswordManager passwordManager,
    [FromRoute] Guid userId,
    [FromQuery] Guid tenantId)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await passwordManager.AdminResetPassword(resolvedTenantId, userId);
    if (!result.IsSuccess)
    {
      if (string.Equals(result.Reason, "User not found.", StringComparison.Ordinal))
      {
        return NotFound();
      }

      return BadRequest(result.Reason);
    }

    return Ok(new AdminResetPasswordResponseDto(result.Value.TemporaryPassword));
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  [ProducesResponseType<UserResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UserResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromServices] IUserCreator userCreator,
    [FromQuery] Guid tenantId,
    [FromBody] CreateUserRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    // A server principal's tenant claim check is a no-op, so without this existence check a
    // create against a tenant that does not (or no longer) exists would write a user row that
    // no tenant owns.
    if (!await appDb.Tenants.AnyAsync(x => x.Id == resolvedTenantId, HttpContext.RequestAborted))
    {
      return BadRequest("Tenant not found.");
    }

    var presetNames = request.PresetNames?.ToArray();
    if (presetNames is { Length: > 0 })
    {
      var missingPresets = presetNames.Except(PermissionPresets.All.Keys).ToList();
      if (missingPresets.Count != 0)
      {
        return BadRequest($"Presets not found: {string.Join(',', missingPresets)}");
      }

      var requiresServerAdminPreset = presetNames.Contains(PermissionPresets.ServerAdministrator);
      var requiresTenantAdmin = presetNames.Contains(PermissionPresets.TenantAdministrator);
      // Presets seed device permissions at the broadest tenant-legal scope (Tenant), so a preset
      // that grants device permissions still creates tenant-scoped grants and requires tenant
      // permission management. Use GetBroadestTenantLegalScope to match the actual seeding scope,
      // not GetBroadestLegalScope (which returns Server for device permissions).
      var requiresTenantPermissionManagement = presetNames
        .SelectMany(PermissionPresets.GetPermissions)
        .Any(permissionName =>
          PermissionCatalog.GetBroadestTenantLegalScope(permissionName) == PermissionScopeKind.Tenant);

      if (requiresServerAdminPreset || requiresTenantPermissionManagement)
      {
        var callerPrincipal = User.ToPrincipalDescriptor();
        if (callerPrincipal is null)
        {
          return BadRequest("Caller principal not found.");
        }

        var serverResource = new ResourceDescriptor(PermissionScopeKind.Server);
        var tenantResource = new ResourceDescriptor(
          PermissionScopeKind.Tenant,
          resolvedTenantId,
          resolvedTenantId);
        // Granting the ServerAdministrator preset is a server permission-management action, so
        // the authority check is ServerPermissionsWrite (the same permission that governs
        // creating server-scoped assignments), not a blanket admin knob.
        var requestServerPermsWrite = new PermissionEvaluationRequest(
          PermissionNames.ServerPermissionsWrite,
          serverResource);

        var requestTenantPermsWrite = new PermissionEvaluationRequest(
          PermissionNames.TenantPermissionsWrite,
          tenantResource);

        var requestTenantPermsDeny = new PermissionEvaluationRequest(
          PermissionNames.TenantPermissionsDeny,
          tenantResource);

        var decisions = await permissionEvaluator.EvaluateBatch(
          callerPrincipal,
          [requestServerPermsWrite, requestTenantPermsWrite, requestTenantPermsDeny],
          HttpContext.RequestAborted);

        var hasServerPermsWrite = decisions[requestServerPermsWrite].Allowed;
        var hasTenantWrite = decisions[requestTenantPermsWrite].Allowed;
        var hasTenantDeny = decisions[requestTenantPermsDeny].Allowed;

        if (requiresServerAdminPreset && !hasServerPermsWrite)
        {
          return Forbid();
        }

        if (requiresTenantPermissionManagement && !hasServerPermsWrite && !hasTenantWrite)
        {
          return Forbid();
        }

        if (requiresTenantAdmin && !hasServerPermsWrite && !(hasTenantWrite && hasTenantDeny))
        {
          return Forbid();
        }
      }
    }

    var createResult = await userCreator.CreateUser(
      string.IsNullOrWhiteSpace(request.Email) ? request.UserName : request.Email,
      request.Password ?? string.Empty,
      resolvedTenantId,
      presetNames,
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

    var createdAt = await appDb.Users
      .Where(x => x.Id == user.Id)
      .Select(x => x.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);
    var permissions = await appDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id &&
                  x.PrincipalKind == PermissionPrincipalKind.User &&
                  x.Effect == PermissionEffect.Allow &&
                  x.IsEnabled)
      .Select(x => x.PermissionName)
      .Distinct()
      .ToListAsync(cancellationToken);

    var response = new UserResponseDto(user.Id, user.UserName, user.Email, createdAt, [.. permissions]);
    return CreatedAtAction(nameof(GetAll), new { tenantId = resolvedTenantId }, response);
  }

  [HttpPost("{userId:guid}/personal-access-tokens")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersWrite)]
  [ProducesResponseType<CreatePersonalAccessTokenResponseDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromQuery] Guid tenantId,
    [FromBody] CreatePersonalAccessTokenRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == resolvedTenantId, cancellationToken);

    if (!targetExists)
    {
      return NotFound();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await personalAccessTokenManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto(
        request.Name,
        request.PermissionMode,
        request.Scopes
          ?.Select(scope => new InternalDtos.CredentialScopeDto(
            scope.PermissionName,
            scope.ScopeKind,
            scope.ScopeId))
          .ToList()),
      userId,
      actor);

    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    var response = new CreatePersonalAccessTokenResponseDto(
      ToV1ResponseDto(result.Value.PersonalAccessToken),
      result.Value.PlainTextToken);

    return CreatedAtAction(
      nameof(GetUserPersonalAccessTokens),
      new { userId, tenantId = resolvedTenantId },
      response);
  }

  [HttpDelete("{userId:guid}")]
  [Authorize(Policy = PolicyNames.RequireTenantUsersDelete)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.TryGetUserId(out var callerUserId) && callerUserId == userId)
    {
      return BadRequest("You cannot delete your own account. Use the identity-management pages instead.");
    }

    var user = await appDb.Users
      .Include(x => x.UserPreferences)
      .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == resolvedTenantId, cancellationToken);

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
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DeleteUserPersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == resolvedTenantId, cancellationToken);

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
  [Authorize(Policy = PolicyNames.RequireUsersRead)]
  [ProducesResponseType<UsersResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UsersResponseDto>> GetAll(
    [FromServices] AppDb appDb,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var users = await appDb.Users
      .Where(x => x.TenantId == resolvedTenantId)
      .OrderBy(x => x.UserName)
      .ThenBy(x => x.Id)
      .Select(x => new { x.Id, x.UserName, x.Email, x.CreatedAt })
      .ToListAsync(cancellationToken);

    var userIds = users.Select(x => x.Id).ToList();

    var displayNames = await appDb.UserPreferences
      .Where(x => userIds.Contains(x.UserId) && x.Name == UserPreferenceNames.UserDisplayName)
      .Select(x => new { x.UserId, x.Value })
      .ToListAsync(cancellationToken);

    var displayNamesLookup = displayNames.ToDictionary(x => x.UserId, x => x.Value);

    var permissionsByUser = await appDb.PermissionAssignments
      .Where(x => userIds.Contains(x.PrincipalId) &&
                  x.PrincipalKind == PermissionPrincipalKind.User &&
                  x.Effect == PermissionEffect.Allow &&
                  x.IsEnabled)
      .Select(x => new { x.PrincipalId, x.PermissionName })
      .ToListAsync(cancellationToken);

    var permissionsLookup = permissionsByUser
      .GroupBy(x => x.PrincipalId)
      .ToDictionary(group => group.Key, group => group.Select(x => x.PermissionName).Distinct().ToList());

    var items = users
      .Select(x => new UserResponseDto(
        x.Id, x.UserName, x.Email, x.CreatedAt,
        permissionsLookup.GetValueOrDefault(x.Id) ?? [],
        displayNamesLookup.GetValueOrDefault(x.Id)))
      .ToList();

    return Ok(new UsersResponseDto { Items = items });
  }

  [HttpGet("{userId:guid}/personal-access-tokens")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersRead)]
  [ProducesResponseType<PersonalAccessTokensResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PersonalAccessTokensResponseDto>> GetUserPersonalAccessTokens(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == resolvedTenantId, cancellationToken);

    if (!targetExists)
    {
      return NotFound();
    }

    var tokens = await personalAccessTokenManager.GetForUser(userId);
    return Ok(new PersonalAccessTokensResponseDto
    {
      Items = [.. tokens.Select(ToV1ResponseDto)]
    });
  }

  [HttpPut("{userId:guid}/personal-access-tokens/{tokenId:guid}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersWrite)]
  [ProducesResponseType<PersonalAccessTokenResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromQuery] Guid tenantId,
    [FromBody] UpdatePersonalAccessTokenRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var targetExists = await appDb.Users
      .AnyAsync(x => x.Id == userId && x.TenantId == resolvedTenantId, cancellationToken);

    if (!targetExists)
    {
      return NotFound();
    }

    var result = await personalAccessTokenManager.Update(
      tokenId,
      new InternalDtos.UpdatePersonalAccessTokenRequestDto(request.Name),
      userId);

    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(ToV1ResponseDto(result.Value));
  }

  private static PersonalAccessTokenResponseDto ToV1ResponseDto(
    InternalDtos.PersonalAccessTokenResponseDto token)
  {
    return new PersonalAccessTokenResponseDto(
      token.Id,
      token.Name,
      token.CreatedAt,
      token.LastUsed,
      token.PermissionCount,
      token.PermissionMode);
  }
}

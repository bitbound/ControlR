using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using ControlR.Web.Server.Services.Users;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.UsersEndpoint)]
[ApiController]
[Authorize]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class UsersController : ControllerBase
{
  [ApiDeprecated("/api/v1/users/{userId}/reset-password?tenantId=", Note = "The replacement requires tenantId as a query parameter instead of deriving it from the caller claim.")]
  [HttpPost("{userId:guid}/reset-password")]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  public async Task<ActionResult<InternalDtos.AdminResetPasswordResponseDto>> AdminResetPassword(
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

  [ApiDeprecated("/api/v1/users?tenantId=", Note = "The replacement requires tenantId as a query parameter instead of deriving it from the caller claim.")]
  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireTenantUsersWrite)]
  public async Task<ActionResult<InternalDtos.UserResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromServices] IPermissionEvaluator permissionEvaluator,
    [FromServices] IUserCreator userCreator,
    [FromBody] InternalDtos.CreateUserRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
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
          tenantId,
          tenantId);
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
      tenantId,
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
      .FirstOrDefaultAsync();
    var permissions = await appDb.PermissionAssignments
      .Where(x => x.PrincipalId == user.Id &&
                  x.PrincipalKind == PermissionPrincipalKind.User &&
                  x.Effect == PermissionEffect.Allow &&
                  x.IsEnabled)
      .Select(x => x.PermissionName)
      .Distinct()
      .ToListAsync();

    var response = new InternalDtos.UserResponseDto(user.Id, user.UserName, user.Email, createdAt, [.. permissions]);
    return CreatedAtAction(nameof(GetAll), new { id = user.Id }, response);
  }

  [ApiDeprecated("/api/v1/users/{userId}/personal-access-tokens?tenantId=", Note = "The replacement requires tenantId as a query parameter and returns 201.")]
  [HttpPost("{userId:guid}/personal-access-tokens")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersWrite)]
  public async Task<ActionResult<InternalDtos.CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.CreatePersonalAccessTokenRequestDto request)
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

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return BadRequest("User ID not found.");
    }

    var result = await personalAccessTokenManager.CreateToken(request, userId, actor);
    if (!result.IsSuccess)
    {
      return BadRequest(result.Reason);
    }

    return Ok(result.Value);
  }

  [ApiDeprecated("/api/v1/users/{userId}?tenantId=", Note = "The replacement requires tenantId as a query parameter instead of deriving it from the caller claim.")]
  [HttpDelete("{userId:guid}")]
  [Authorize(Policy = PolicyNames.RequireTenantUsersDelete)]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid userId,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    if (User.TryGetUserId(out var callerUserId) && callerUserId == userId)
    {
      return BadRequest("You cannot delete your own account. Use the identity-management pages instead.");
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

  [ApiDeprecated("/api/v1/users/{userId}/personal-access-tokens/{tokenId}?tenantId=", Note = "The replacement requires tenantId as a query parameter.")]
  [HttpDelete("{userId:guid}/personal-access-tokens/{tokenId:guid}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersWrite)]
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

  [ApiDeprecated("/api/v1/users?tenantId=", Note = "The replacement requires tenantId as a query parameter and returns an Items envelope.")]
  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireUsersRead)]
  public async Task<ActionResult<List<InternalDtos.UserResponseDto>>> GetAll(
    [FromServices] AppDb appDb)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return BadRequest("User tenant not found.");
    }

    var users = await appDb.Users
      .Where(x => x.TenantId == tenantId)
      .OrderBy(x => x.UserName)
      .ThenBy(x => x.Id)
      .Select(x => new { x.Id, x.UserName, x.Email, x.CreatedAt })
      .ToListAsync();

    var userIds = users.Select(x => x.Id).ToList();

    var displayNames = await appDb.UserPreferences
      .Where(x => userIds.Contains(x.UserId) && x.Name == UserPreferenceNames.UserDisplayName)
      .Select(x => new { x.UserId, x.Value })
      .ToListAsync();

    var displayNamesLookup = displayNames.ToDictionary(x => x.UserId, x => x.Value);

    var permissionsByUser = await appDb.PermissionAssignments
      .Where(x => userIds.Contains(x.PrincipalId) &&
                  x.PrincipalKind == PermissionPrincipalKind.User &&
                  x.Effect == PermissionEffect.Allow &&
                  x.IsEnabled)
      .Select(x => new { x.PrincipalId, x.PermissionName })
      .ToListAsync();

    var permissionsLookup = permissionsByUser
      .GroupBy(x => x.PrincipalId)
      .ToDictionary(group => group.Key, group => group.Select(x => x.PermissionName).Distinct().ToList());

    return users
      .Select(x => new InternalDtos.UserResponseDto(
        x.Id, x.UserName, x.Email, x.CreatedAt,
        permissionsLookup.GetValueOrDefault(x.Id) ?? [],
        displayNamesLookup.GetValueOrDefault(x.Id)))
      .ToList();
  }

  [ApiDeprecated("/api/v1/users/{userId}/personal-access-tokens?tenantId=", Note = "The replacement requires tenantId as a query parameter.")]
  [HttpGet("{userId:guid}/personal-access-tokens")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersRead)]
  public async Task<ActionResult<IEnumerable<InternalDtos.PersonalAccessTokenResponseDto>>> GetUserPersonalAccessTokens(
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

  [ApiDeprecated("/api/v1/users/{userId}/personal-access-tokens/{tokenId}?tenantId=", Note = "The replacement requires tenantId as a query parameter.")]
  [HttpPut("{userId:guid}/personal-access-tokens/{tokenId:guid}")]
  [Authorize(Policy = PolicyNames.RequirePersonalAccessTokensOthersWrite)]
  public async Task<ActionResult<InternalDtos.PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(
    [FromRoute] Guid userId,
    [FromRoute] Guid tokenId,
    [FromServices] IPersonalAccessTokenManager personalAccessTokenManager,
    [FromServices] AppDb appDb,
    [FromBody] InternalDtos.UpdatePersonalAccessTokenRequestDto request)
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

using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Manages personal access tokens and their credential scopes.
/// </summary>
public interface IPersonalAccessTokenManager
{
  Task<Result<InternalDtos.CreatePersonalAccessTokenResponseDto>> CreateToken(InternalDtos.CreatePersonalAccessTokenRequestDto request, Guid userId, PrincipalDescriptor actor);

  /// <summary>
  /// Creates a token with a pre-specified secret and ID for bootstrap scenarios where the
  /// token must be known ahead of time. The caller is responsible for logging the secret.
  /// The permission mode is required explicitly and never inferred.
  /// </summary>
  Task<Result<InternalDtos.PersonalAccessTokenResponseDto>> CreateTokenWithKey(
    Guid tokenId, string secret, string name, Guid userId, PersonalAccessTokenPermissionMode permissionMode);

  Task<Result> Delete(Guid id, Guid userId);

  Task<IEnumerable<InternalDtos.PersonalAccessTokenResponseDto>> GetForUser(Guid userId);

  Task<Result<InternalDtos.PersonalAccessTokenResponseDto>> Update(Guid id, InternalDtos.UpdatePersonalAccessTokenRequestDto request, Guid userId);

  /// <summary>
  /// Validates a token of the form <c>{hex-guid}:{secret}</c> and returns its user/tenant.
  /// </summary>
  Task<Result<PersonalAccessTokenValidationResult>> ValidateToken(string token);
}

public class PersonalAccessTokenManager(
  AppDb appDb,
  TimeProvider timeProvider,
  IPasswordHasher<string> passwordHasher,
  ICredentialScopeService credentialScopeService,
  IAuthorizationChangeLogFactory changeLogFactory) : IPersonalAccessTokenManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;
  private readonly ICredentialScopeService _credentialScopeService = credentialScopeService;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<Result<InternalDtos.CreatePersonalAccessTokenResponseDto>> CreateToken(InternalDtos.CreatePersonalAccessTokenRequestDto request, Guid userId, PrincipalDescriptor actor)
  {
    try
    {
      if (!Enum.IsDefined(request.PermissionMode))
      {
        return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>("PermissionMode is not a valid value.");
      }

      // Credential laundering guard: an InheritOwner token evaluates as the owner's full
      // effective permissions, so it may only be minted by a full-identity session (cookie,
      // bearer, or service-account credential). A credential-scoped principal (PAT or logon
      // token) may mint only Restricted tokens, whose scopes are validated against the owner.
      if (request.PermissionMode == PersonalAccessTokenPermissionMode.InheritOwner &&
          actor.IsCredentialScoped)
      {
        return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>(
          "InheritOwner tokens may only be created by a full-identity session. Use the Restricted mode with explicit scopes.");
      }

      var scopes = request.Scopes;
      var hasScopes = scopes is { Count: > 0 };

      if (request.PermissionMode == PersonalAccessTokenPermissionMode.InheritOwner && hasScopes)
      {
        return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>(
          "Scopes are not meaningful for an inherit-owner token. Omit scopes or use the restricted mode.");
      }

      Guid? ownerTenantId = null;

      IReadOnlyList<InternalDtos.CredentialScopeDto>? requestScopes = null;
      if (scopes is { Count: > 0 } scopeRows)
      {
        requestScopes = scopeRows;
        var owner = await _appDb.Users
          .IgnoreQueryFilters()
          .AsNoTracking()
          .FirstOrDefaultAsync(x => x.Id == userId);
        if (owner is null || owner.TenantId == Guid.Empty)
        {
          return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>("Token owner not found.");
        }

        ownerTenantId = owner.TenantId;

        var ownerPrincipal = new PrincipalDescriptor(
          PrincipalType: PrincipalType.User,
          PrincipalId: userId,
          TenantId: owner.TenantId,
          AuthMethod: "pat-scope-validation");

        var scopeValidation = await _credentialScopeService.ValidateGrantableScopes(
          ownerPrincipal, owner.TenantId, requestScopes);
        if (!scopeValidation.IsSuccess)
        {
          return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>(scopeValidation.Reason);
        }
      }

      var plainTextKey = RandomGenerator.CreateApiKey();
      var hashedKey = _passwordHasher.HashPassword(string.Empty, plainTextKey);

      var personalAccessToken = new PersonalAccessToken
      {
        Name = request.Name,
        HashedKey = hashedKey,
        UserId = userId,
        PermissionMode = request.PermissionMode
      };

      _appDb.PersonalAccessTokens.Add(personalAccessToken);

      // Wrap the token, scope rows, and change log in a transaction (relational providers
      // only) so they commit atomically. The token Id is database-generated, so the scope
      // rows and change log can only reference it after the first save.
      await using var transaction = _appDb.Database.IsRelational()
        ? await _appDb.Database.BeginTransactionAsync()
        : null;

      await _appDb.SaveChangesAsync();

      var tenantId = hasScopes ? ownerTenantId : null;

      if (hasScopes && tenantId is { } scopeTenantId && requestScopes is { } nonNullScopes)
      {
        foreach (var scope in nonNullScopes)
        {
          _appDb.PermissionAssignments.Add(PermissionAssignment.CreateGrant(
            PermissionPrincipalKind.PersonalAccessToken,
            personalAccessToken.Id,
            scope.PermissionName,
            scope.ScopeKind,
            scope.ScopeId,
            scopeTenantId,
            actor));
        }

        _appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
          AuthorizationChangeLogActions.CredentialScopeSet,
          actor,
          AuthorizationChangeLogTargetTypes.PersonalAccessToken,
          personalAccessToken.Id,
          scopeTenantId,
          after: new CredentialScopeSetSummary(nonNullScopes.Count)));

        await _appDb.SaveChangesAsync();
      }

      if (transaction is not null)
      {
        await transaction.CommitAsync();
      }

      var hexId = Convert.ToHexString(personalAccessToken.Id.ToByteArray());
      var combinedKey = $"{hexId}:{plainTextKey}";
      var permissionCount = requestScopes?.Select(x => x.PermissionName).Distinct().Count() ?? 0;
      var response = new InternalDtos.CreatePersonalAccessTokenResponseDto(MapToDto(personalAccessToken, permissionCount), combinedKey);
      return Result.Ok(response);
    }
    catch (Exception ex)
    {
      return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>(ex, "Failed to create personal access token.");
    }
  }

  public async Task<Result<InternalDtos.PersonalAccessTokenResponseDto>> CreateTokenWithKey(
    Guid tokenId, string secret, string name, Guid userId, PersonalAccessTokenPermissionMode permissionMode)
  {
    if (tokenId == Guid.Empty)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>("Token ID cannot be empty.");
    }

    if (string.IsNullOrWhiteSpace(secret))
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>("Secret cannot be empty.");
    }

    if (secret.Length < 32)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>("PAT secret must be at least 32 characters.");
    }

    if (!Enum.IsDefined(permissionMode))
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>("PermissionMode is not a valid value.");
    }

    try
    {
      if (await _appDb.PersonalAccessTokens.IgnoreQueryFilters().AnyAsync(x => x.Id == tokenId))
      {
        return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>("A personal access token with this ID already exists.");
      }

      var hashedKey = _passwordHasher.HashPassword(string.Empty, secret);
      var personalAccessToken = new PersonalAccessToken
      {
        Id = tokenId,
        Name = name,
        HashedKey = hashedKey,
        UserId = userId,
        PermissionMode = permissionMode
      };

      _appDb.PersonalAccessTokens.Add(personalAccessToken);
      await _appDb.SaveChangesAsync();

      return Result.Ok(MapToDto(personalAccessToken, 0));
    }
    catch (Exception ex)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>(ex, "Failed to create personal access token with pre-keyed secret.");
    }
  }

  public async Task<Result> Delete(Guid id, Guid userId)
  {
    try
    {
      var personalAccessToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

      if (personalAccessToken is null)
      {
        return Result.Fail("Personal access token not found.");
      }

      // Remove the token's assignment rows in the same unit of work as the token row.
      // PermissionAssignment is a polymorphic principal with no FK cascade, so a new PAT
      // reusing the same ID would otherwise inherit the deleted token's scopes.
      await using var transaction = _appDb.Database.IsRelational()
        ? await _appDb.Database.BeginTransactionAsync()
        : null;

      var assignments = await _appDb.PermissionAssignments
        .IgnoreQueryFilters()
        .Where(x => x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                    x.PrincipalId == id)
        .ToListAsync();

      _appDb.PermissionAssignments.RemoveRange(assignments);
      _appDb.PersonalAccessTokens.Remove(personalAccessToken);
      await _appDb.SaveChangesAsync();

      if (transaction is not null)
      {
        await transaction.CommitAsync();
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex, "Failed to delete personal access token.");
    }
  }

  public async Task<IEnumerable<InternalDtos.PersonalAccessTokenResponseDto>> GetForUser(Guid userId)
  {
    var personalAccessTokens = await _appDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .Where(x => x.UserId == userId)
      .OrderByDescending(x => x.CreatedAt)
      .ToListAsync();

    var tokenIds = personalAccessTokens.Select(x => x.Id).ToList();
    var permissionCountsByToken = await GetPermissionCountLookup(tokenIds);

    return personalAccessTokens
      .Select(x => MapToDto(x, permissionCountsByToken.GetValueOrDefault(x.Id)))
      .ToList();
  }

  public async Task<Result<InternalDtos.PersonalAccessTokenResponseDto>> Update(Guid id, InternalDtos.UpdatePersonalAccessTokenRequestDto request, Guid userId)
  {
    try
    {
      var personalAccessToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

      if (personalAccessToken is null)
      {
        return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>("Personal access token not found.");
      }

      personalAccessToken.Name = request.Name;
      await _appDb.SaveChangesAsync();

      var permissionsLookup = await GetPermissionCountLookup([id]);
      return Result.Ok(MapToDto(personalAccessToken, permissionsLookup.GetValueOrDefault(id)));
    }
    catch (Exception ex)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenResponseDto>(ex, "Failed to update personal access token.");
    }
  }

  public async Task<Result<PersonalAccessTokenValidationResult>> ValidateToken(string token)
  {
    try
    {
      var parts = token.Split(':', 2);
      if (parts.Length != 2)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token format.");
      }

      var tokenIdBytes = Convert.FromHexString(parts[0]);
      var tokenId = new Guid(tokenIdBytes);
      
      var storedToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == tokenId);

      if (storedToken is null)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token.");
      }

      if (storedToken.RevokedAt is not null)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Personal access token has been revoked.");
      }

      if (storedToken.ExpiresAt is not null && storedToken.ExpiresAt <= _timeProvider.GetUtcNow())
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Personal access token has expired.");
      }

      var verification = _passwordHasher.VerifyHashedPassword(string.Empty, storedToken.HashedKey, parts[1]);

      if (verification == PasswordVerificationResult.Failed)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token.");
      }

      if (verification == PasswordVerificationResult.SuccessRehashNeeded)
      {
        storedToken.HashedKey = _passwordHasher.HashPassword(string.Empty, parts[1]);
      }

      storedToken.LastUsed = _timeProvider.GetUtcNow();
      await _appDb.SaveChangesAsync();

      var result = PersonalAccessTokenValidationResult.Success(storedToken.Id, storedToken.UserId);
      return Result.Ok(result);
    }
    catch (Exception ex)
    {
      return Result.Fail<PersonalAccessTokenValidationResult>(ex, "Failed to validate personal access token.");
    }
  }

  private static InternalDtos.PersonalAccessTokenResponseDto MapToDto(
    PersonalAccessToken personalAccessToken,
    int permissionCount)
  {
    return new InternalDtos.PersonalAccessTokenResponseDto(
      personalAccessToken.Id,
      personalAccessToken.Name,
      personalAccessToken.CreatedAt,
      personalAccessToken.LastUsed,
      permissionCount,
      personalAccessToken.PermissionMode);
  }

  private async Task<Dictionary<Guid, int>> GetPermissionCountLookup(IReadOnlyCollection<Guid> tokenIds)
  {
    if (tokenIds.Count == 0)
    {
      return [];
    }

    var counts = await _appDb.PermissionAssignments
      .Where(x => tokenIds.Contains(x.PrincipalId) &&
                  x.PrincipalKind == PermissionPrincipalKind.PersonalAccessToken &&
                  x.Effect == PermissionEffect.Allow &&
                  x.IsEnabled)
      .GroupBy(x => x.PrincipalId)
      .Select(g => new
      {
        g.Key,
        Count = g.Select(x => x.PermissionName).Distinct().Count()
      })
      .ToDictionaryAsync(x => x.Key, x => x.Count);

    return counts;
  }
}

using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Primitives;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.ServiceAccounts;

/// <summary>
/// Manages server-scoped service accounts and their credentials: bootstrap from
/// configuration, CRUD, credential creation/revocation, and credential validation for the
/// service-account authentication handler.
/// </summary>
public interface IServiceAccountManager
{
  /// <summary>
  /// Adds a new credential to an existing server service account. Returns the credential
  /// metadata and the plaintext secret, which is only exposed this once.
  /// </summary>
  Task<HttpResult<CreateServiceAccountCredentialResponseDto>> AddCredential(Guid serviceAccountId, string name, CancellationToken cancellationToken);

  /// <summary>
  /// Creates the bootstrapped server service account and its initial credential when the
  /// bootstrap options are fully supplied. Skips creation when the named account already exists.
  /// Throws when the bootstrap input is only partially configured.
  /// </summary>
  Task<HttpResult> BootstrapServerServiceAccount(CancellationToken cancellationToken);

  /// <summary>
  /// Creates a new server-scoped service account and its first credential. Returns the new
  /// account and the plaintext secret, which is only exposed this once.
  /// </summary>
  Task<HttpResult<CreateServiceAccountResponseDto>> CreateForServer(string name, string? description, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a server service account. Credentials cascade-delete.
  /// </summary>
  /// <param name="serviceAccountId">The ID of the service account to delete.</param>
  /// <param name="requestingPrincipalId">The ID of the authenticated principal making the request. Used to prevent self-deletion.</param>
  /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
  Task<HttpResult> Delete(Guid serviceAccountId, Guid requestingPrincipalId, CancellationToken cancellationToken);

  /// <summary>
  /// Returns a single server-scoped service account with its credential metadata.
  /// </summary>
  Task<HttpResult<ServiceAccountDto>> Get(Guid serviceAccountId, CancellationToken cancellationToken);

  /// <summary>
  /// Returns all server-scoped service accounts with their credential metadata.
  /// </summary>
  Task<List<ServiceAccountDto>> GetAllForServer(CancellationToken cancellationToken);

  /// <summary>
  /// Revokes a credential by setting <see cref="ServiceAccountCredential.RevokedAt"/>.
  /// </summary>
  Task<HttpResult> RevokeCredential(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken);

  /// <summary>
  /// Validates a <c>{hex_id}:{plaintext_secret}</c> API key against a service account credential.
  /// On success updates <see cref="ServiceAccountCredential.LastUsedAt"/> and returns the
  /// owning service account and the credential. Revoked, expired, disabled-account, and
  /// nonexistent-or-invalid credentials all fail.
  /// </summary>
  Task<HttpResult<ServiceAccountCredentialValidationResult>> ValidateCredential(string apiKey, CancellationToken cancellationToken);
}

public sealed record ServiceAccountCredentialValidationResult(
  ServiceAccount ServiceAccount,
  ServiceAccountCredential Credential);

public class ServiceAccountManager(
  AppDb appDb,
  TimeProvider timeProvider,
  IPasswordHasher<string> passwordHasher,
  IMemoryCache memoryCache,
  IOptionsMonitor<BootstrapOptions> bootstrapOptions,
  ILogger<ServiceAccountManager> logger) : IServiceAccountManager
{
  private const string InvalidApiKeyFormatMessage = "Invalid service account API key format.";
  private const string InvalidCredentialMessage = "Invalid service account credential.";
  private const int MinimumSecretLength = 32;

  private static readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);

  public async Task<HttpResult<CreateServiceAccountCredentialResponseDto>> AddCredential(
    Guid serviceAccountId,
    string name,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResponseDto>(HttpResultErrorCode.BadRequest, "Credential name is required.");
    }

    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResponseDto>(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    if (!account.IsEnabled)
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResponseDto>(HttpResultErrorCode.Forbidden, "Service account is disabled.");
    }

    var plainTextSecret = RandomGenerator.CreateApiKey();
    var hashedSecret = passwordHasher.HashPassword(string.Empty, plainTextSecret);

    var credential = new ServiceAccountCredential
    {
      Name = name,
      HashedSecret = hashedSecret
    };
    account.Credentials.Add(credential);
    await appDb.SaveChangesAsync(cancellationToken);

    var apiKey = FormatApiKey(credential.Id, plainTextSecret);
    return HttpResult.Ok(new CreateServiceAccountCredentialResponseDto(MapCredentialToDto(credential), apiKey));
  }

  public async Task<HttpResult> BootstrapServerServiceAccount(
    CancellationToken cancellationToken)
  {
    var name = bootstrapOptions.CurrentValue.ServerServiceAccountName;
    var tokenId = bootstrapOptions.CurrentValue.ServerServiceAccountTokenId;
    var secret = bootstrapOptions.CurrentValue.ServerServiceAccountTokenSecret;
    var description = bootstrapOptions.CurrentValue.ServerServiceAccountDescription;
    var accountId = bootstrapOptions.CurrentValue.ServerServiceAccountId;

    var nameSet = !string.IsNullOrWhiteSpace(name);
    var tokenIdSet = tokenId.HasValue;
    var secretSet = !string.IsNullOrWhiteSpace(secret);

    if (!nameSet && !tokenIdSet && !secretSet)
    {
      logger.LogInformation("Bootstrap server service account skipped: not configured.");
      return HttpResult.Ok();
    }

    // Any subset configured is a partial configuration error.
    if (!nameSet || !tokenIdSet || !secretSet)
    {
      logger.LogError(
        "Bootstrap server service account configuration incomplete. Name configured: {NameIsSet}, " +
        "TokenId configured: {TokenIdIsSet}, Secret configured: {SecretIsSet}. All three must be set.",
        nameSet,
        tokenIdSet,
        secretSet);
      throw new InvalidOperationException(
        "Bootstrap server service account configuration is incomplete: " +
        "ServerServiceAccountName, ServerServiceAccountTokenId, and ServerServiceAccountTokenSecret must all be configured.");
    }

    Guard.IsNotNull(name);
    Guard.IsNotNull(tokenId);
    Guard.IsNotNull(secret);

    var credentialId = tokenId.Value;

    if (secret.Length < MinimumSecretLength)
    {
      logger.LogError("Bootstrap server service account creation failed: ServerServiceAccountTokenSecret must be at least {Length} characters.", MinimumSecretLength);
      throw new InvalidOperationException($"Bootstrap server service account creation failed: ServerServiceAccountTokenSecret must be at least {MinimumSecretLength} characters.");
    }

    var alreadyExists = await appDb.ServiceAccounts
      .AnyAsync(x => x.Kind == ServiceAccountKind.Server && x.Name == name, cancellationToken);

    if (alreadyExists)
    {
      logger.LogInformation("Bootstrap server service account skipped: account '{Name}' already exists.", name);
      return HttpResult.Ok();
    }

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = name,
      Description = description,
      IsEnabled = true
    };

    if (accountId.HasValue)
    {
      account.Id = accountId.Value;
    }

    var hashedSecret = passwordHasher.HashPassword(string.Empty, secret);
    var credential = new ServiceAccountCredential
    {
      Id = credentialId,
      Name = "Bootstrap Credential",
      HashedSecret = hashedSecret
    };
    account.Credentials.Add(credential);

    appDb.ServiceAccounts.Add(account);
    await appDb.SaveChangesAsync(cancellationToken);

    // Log by credential id only, never the secret.
    logger.LogInformation(
      "Bootstrap server service account '{Name}' created with credential id {CredentialId}.",
      name,
      credential.Id);
    return HttpResult.Ok();
  }

  public async Task<HttpResult<CreateServiceAccountResponseDto>> CreateForServer(
    string name,
    string? description,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<CreateServiceAccountResponseDto>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var nameConflict = await appDb.ServiceAccounts
      .AnyAsync(x => x.Kind == ServiceAccountKind.Server && x.Name == name, cancellationToken);
    if (nameConflict)
    {
      return HttpResult.Fail<CreateServiceAccountResponseDto>(HttpResultErrorCode.Conflict, "A server service account with that name already exists.");
    }

    var plainTextSecret = RandomGenerator.CreateApiKey();
    var hashedSecret = passwordHasher.HashPassword(string.Empty, plainTextSecret);

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = name,
      Description = description,
      IsEnabled = true
    };

    var credential = new ServiceAccountCredential
    {
      Name = "Initial Credential",
      HashedSecret = hashedSecret
    };
    account.Credentials.Add(credential);

    appDb.ServiceAccounts.Add(account);

    var saveResult = await appDb.SaveChangesOrConfirmConflictAsync<ServiceAccount>(
      x => x.Kind == ServiceAccountKind.Server && x.Name == name,
      cancellationToken);

    if (saveResult == SaveChangesResult.ConflictDetected)
    {
      return HttpResult.Fail<CreateServiceAccountResponseDto>(HttpResultErrorCode.Conflict, "A server service account with that name already exists.");
    }

    var apiKey = FormatApiKey(credential.Id, plainTextSecret);
    return HttpResult.Ok(new CreateServiceAccountResponseDto(MapToDto(account), apiKey));
  }

  public async Task<HttpResult> Delete(Guid serviceAccountId, Guid requestingPrincipalId, CancellationToken cancellationToken)
  {
    if (serviceAccountId.Equals(requestingPrincipalId))
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden, "A service account cannot delete itself.");
    }

    var account = await appDb.ServiceAccounts
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    await EvictAccountFromCacheAsync(serviceAccountId, cancellationToken);

    appDb.ServiceAccounts.Remove(account);
    await appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult<ServiceAccountDto>> Get(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<ServiceAccountDto>(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    return HttpResult.Ok(MapToDto(account));
  }

  public async Task<List<ServiceAccountDto>> GetAllForServer(CancellationToken cancellationToken)
  {
    var accounts = await appDb.ServiceAccounts
      .Where(x => x.Kind == ServiceAccountKind.Server)
      .Include(x => x.Credentials)
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return [.. accounts.Select(MapToDto)];
  }

  public async Task<HttpResult> RevokeCredential(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken)
  {
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(
        x => x.Id == credentialId && x.ServiceAccountId == serviceAccountId,
        cancellationToken);

    if (credential is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Credential not found.");
    }

    if (credential.RevokedAt is not null)
    {
      return HttpResult.Ok();
    }

    credential.RevokedAt = timeProvider.GetUtcNow();
    await appDb.SaveChangesAsync(cancellationToken);

    EvictCredentialFromCache(credentialId);
    return HttpResult.Ok();
  }

  public async Task<HttpResult<ServiceAccountCredentialValidationResult>> ValidateCredential(
    string apiKey,
    CancellationToken cancellationToken)
  {
    var parts = apiKey.Split(':', 2);
    if (parts.Length != 2)
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.BadRequest, InvalidApiKeyFormatMessage);
    }

    // The header id is the credential Guid rendered via Convert.ToHexString on the
    // Guid's byte array. Reconstruct the Guid from the hex bytes rather than Guid.TryParse.
    Guid credentialId;
    try
    {
      var idBytes = Convert.FromHexString(parts[0]);
      credentialId = new Guid(idBytes);
    }
    catch
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.BadRequest, InvalidApiKeyFormatMessage);
    }

    if (credentialId == Guid.Empty)
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.BadRequest, InvalidApiKeyFormatMessage);
    }

    if (memoryCache.TryGetValue<ServiceAccountCredentialValidationResult>(credentialId, out var cachedResult) && cachedResult is not null)
    {
      var now = timeProvider.GetUtcNow();

      // A credential whose ExpiresAt falls inside the cache window would otherwise keep
      // authenticating for up to the remaining TTL. Re-check and, if expired, evict and
      // fall through to a fresh validation that will reject it.
      if (cachedResult.Credential.ExpiresAt is not null && cachedResult.Credential.ExpiresAt <= now)
      {
        EvictCredentialFromCache(credentialId);
      }
      else
      {
        cachedResult.Credential.LastUsedAt = now;
        await PersistLastUsedAtAsync(credentialId, now, cancellationToken);

        return HttpResult.Ok(cachedResult);
      }
    }

    var credential = await appDb.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .Include(x => x.ServiceAccount)
      .FirstOrDefaultAsync(x => x.Id == credentialId, cancellationToken);

    if (credential is null)
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.Unauthorized, InvalidCredentialMessage);
    }

    var account = credential.ServiceAccount;
    if (account is null || !account.IsEnabled)
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.Forbidden, "Service account is not available.");
    }

    if (credential.RevokedAt is not null)
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.Unauthorized, "Service account credential has been revoked.");
    }

    if (credential.ExpiresAt is not null && credential.ExpiresAt <= timeProvider.GetUtcNow())
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.Unauthorized, "Service account credential has expired.");
    }

    var verification = passwordHasher.VerifyHashedPassword(string.Empty, credential.HashedSecret, parts[1]);
    if (verification == PasswordVerificationResult.Failed)
    {
      return HttpResult.Fail<ServiceAccountCredentialValidationResult>(HttpResultErrorCode.Unauthorized, InvalidCredentialMessage);
    }

    if (verification == PasswordVerificationResult.SuccessRehashNeeded)
    {
      credential.HashedSecret = passwordHasher.HashPassword(string.Empty, parts[1]);
    }

    credential.LastUsedAt = timeProvider.GetUtcNow();
    await appDb.SaveChangesAsync(cancellationToken);

    appDb.Entry(account).State = EntityState.Detached;
    appDb.Entry(credential).State = EntityState.Detached;

    var validationResult = new ServiceAccountCredentialValidationResult(account, credential);
    memoryCache.Set(credentialId, validationResult, _cacheExpiration);

    return HttpResult.Ok(validationResult);
  }

  private static string FormatApiKey(Guid credentialId, string plainTextSecret)
  {
    var hexId = Convert.ToHexString(credentialId.ToByteArray());
    return $"{hexId}:{plainTextSecret}";
  }

  private static ServiceAccountCredentialDto MapCredentialToDto(ServiceAccountCredential credential)
  {
    return new ServiceAccountCredentialDto(
      credential.Id,
      credential.Name,
      credential.CreatedAt,
      credential.ExpiresAt,
      credential.RevokedAt,
      credential.LastUsedAt);
  }

  private static ServiceAccountDto MapToDto(ServiceAccount account)
  {
    return new ServiceAccountDto(
      account.Id,
      account.Name,
      account.Description,
      account.Kind.ToString(),
      account.IsEnabled,
      account.CreatedAt,
      account.Credentials
        .OrderBy(c => c.CreatedAt)
        .ThenBy(c => c.Id)
        .Select(MapCredentialToDto)
        .ToList());
  }

  private async Task EvictAccountFromCacheAsync(Guid serviceAccountId, CancellationToken cancellationToken)
  {
    try
    {
      var account = await appDb.ServiceAccounts
        .AsNoTracking()
        .Include(x => x.Credentials)
        .FirstOrDefaultAsync(x => x.Id == serviceAccountId, cancellationToken);
        
      if (account != null)
      {
        foreach (var cred in account.Credentials)
        {
          memoryCache.Remove(cred.Id);
        }
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex, "Failed to evict cached credential validation results for account {AccountId}.", serviceAccountId);
    }
  }

  private void EvictCredentialFromCache(Guid credentialId)
  {
    memoryCache.Remove(credentialId);
  }

  private async Task PersistLastUsedAtAsync(Guid credentialId, DateTimeOffset now, CancellationToken cancellationToken)
  {
    if (appDb.Database.IsRelational())
    {
      await appDb.ServiceAccountCredentials
        .Where(x => x.Id == credentialId)
        .ExecuteUpdateAsync(x => x.SetProperty(p => p.LastUsedAt, now), cancellationToken);
      return;
    }

    // The EF Core in-memory provider (used by the test suite) does not support
    // ExecuteUpdate. Fall back to a tracked update so service-account auth
    // continues to persist LastUsedAt in tests.
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(x => x.Id == credentialId, cancellationToken);
    if (credential is null)
    {
      return;
    }
    credential.LastUsedAt = now;
    await appDb.SaveChangesAsync(cancellationToken);
  }
}

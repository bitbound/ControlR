using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Options;

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
  Task<Result<CreateServiceAccountCredentialResponseDto>> AddCredentialAsync(Guid serviceAccountId, string name, CancellationToken cancellationToken = default);
  /// <summary>
  /// Creates the bootstrapped server service account and its initial credential when the
  /// bootstrap options are fully supplied. Skips creation when the named account already exists.
  /// Throws when the bootstrap input is only partially configured.
  /// </summary>
  Task<Result> BootstrapServerServiceAccountAsync(BootstrapOptions options, CancellationToken cancellationToken = default);
  /// <summary>
  /// Creates a new server-scoped service account and its first credential. Returns the new
  /// account and the plaintext secret, which is only exposed this once.
  /// </summary>
  Task<Result<CreateServiceAccountResponseDto>> CreateServerAsync(string name, string? description, CancellationToken cancellationToken = default);
  /// <summary>Deletes a server service account. Credentials cascade-delete.</summary>
  Task<Result> DeleteAsync(Guid serviceAccountId, CancellationToken cancellationToken = default);
  /// <summary>Returns all server-scoped service accounts with their credential metadata.</summary>
  Task<List<ServiceAccountDto>> GetAllServerAsync(CancellationToken cancellationToken = default);
  /// <summary>Revokes a credential by setting <see cref="ServiceAccountCredential.RevokedAt"/>.</summary>
  Task<Result> RevokeCredentialAsync(Guid serviceAccountId, Guid credentialId, CancellationToken cancellationToken = default);
  /// <summary>
  /// Validates a <c>{hex_id}:{plaintext_secret}</c> API key against a service account credential.
  /// On success updates <see cref="ServiceAccountCredential.LastUsedAt"/> and returns the
  /// owning service account and the credential. Revoked, expired, disabled-account, and
  /// nonexistent-or-invalid credentials all fail.
  /// </summary>
  Task<Result<ServiceAccountCredentialValidationResult>> ValidateCredentialAsync(string apiKey, CancellationToken cancellationToken = default);
}

public sealed record ServiceAccountCredentialValidationResult(
  ServiceAccount ServiceAccount,
  ServiceAccountCredential Credential);

public class ServiceAccountManager(
  AppDb appDb,
  TimeProvider timeProvider,
  IPasswordHasher<string> passwordHasher,
  ILogger<ServiceAccountManager> logger) : IServiceAccountManager
{
  private const int MinimumSecretLength = 32;

  public async Task<Result<CreateServiceAccountCredentialResponseDto>> AddCredentialAsync(
    Guid serviceAccountId,
    string name,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return Result.Fail<CreateServiceAccountCredentialResponseDto>("Credential name is required.");
    }

    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return Result.Fail<CreateServiceAccountCredentialResponseDto>("Server service account not found.");
    }

    if (!account.IsEnabled)
    {
      return Result.Fail<CreateServiceAccountCredentialResponseDto>("Service account is disabled.");
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
    return Result.Ok(new CreateServiceAccountCredentialResponseDto(MapCredentialToDto(credential), apiKey));
  }

  public async Task<Result> BootstrapServerServiceAccountAsync(
    BootstrapOptions options,
    CancellationToken cancellationToken = default)
  {
    var name = options.ServerServiceAccountName;
    var tokenId = options.ServerServiceAccountTokenId;
    var secret = options.ServerServiceAccountTokenSecret;
    var description = options.ServerServiceAccountDescription;

    var nameSet = !string.IsNullOrWhiteSpace(name);
    var tokenIdSet = !string.IsNullOrWhiteSpace(tokenId);
    var secretSet = !string.IsNullOrWhiteSpace(secret);

    if (!nameSet && !tokenIdSet && !secretSet)
    {
      logger.LogInformation("Bootstrap server service account skipped: not configured.");
      return Result.Ok();
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

    if (!Guid.TryParse(tokenId, out var credentialId) || credentialId == Guid.Empty)
    {
      logger.LogError("Bootstrap server service account creation failed: ServerServiceAccountTokenId is not a valid GUID.");
      throw new InvalidOperationException("Bootstrap server service account creation failed: ServerServiceAccountTokenId must be a valid GUID.");
    }

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
      return Result.Ok();
    }

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = name,
      Description = description,
      IsEnabled = true
    };

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
    return Result.Ok();
  }

  public async Task<Result<CreateServiceAccountResponseDto>> CreateServerAsync(
    string name,
    string? description,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return Result.Fail<CreateServiceAccountResponseDto>("Name is required.");
    }

    var nameConflict = await appDb.ServiceAccounts
      .AnyAsync(x => x.Kind == ServiceAccountKind.Server && x.Name == name, cancellationToken);
    if (nameConflict)
    {
      return Result.Fail<CreateServiceAccountResponseDto>("A server service account with that name already exists.");
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
    await appDb.SaveChangesAsync(cancellationToken);

    var apiKey = FormatApiKey(credential.Id, plainTextSecret);
    return Result.Ok(new CreateServiceAccountResponseDto(MapToDto(account), apiKey));
  }

  public async Task<Result> DeleteAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
  {
    var account = await appDb.ServiceAccounts
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return Result.Fail("Server service account not found.");
    }

    appDb.ServiceAccounts.Remove(account);
    await appDb.SaveChangesAsync(cancellationToken);
    return Result.Ok();
  }

  public async Task<List<ServiceAccountDto>> GetAllServerAsync(CancellationToken cancellationToken = default)
  {
    var accounts = await appDb.ServiceAccounts
      .Where(x => x.Kind == ServiceAccountKind.Server)
      .Include(x => x.Credentials)
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return accounts.Select(MapToDto).ToList();
  }

  public async Task<Result> RevokeCredentialAsync(
    Guid serviceAccountId,
    Guid credentialId,
    CancellationToken cancellationToken = default)
  {
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(
        x => x.Id == credentialId && x.ServiceAccountId == serviceAccountId,
        cancellationToken);

    if (credential is null)
    {
      return Result.Fail("Credential not found.");
    }

    if (credential.RevokedAt is not null)
    {
      return Result.Ok(); // Idempotent.
    }

    credential.RevokedAt = timeProvider.GetUtcNow();
    await appDb.SaveChangesAsync(cancellationToken);
    return Result.Ok();
  }

  public async Task<Result<ServiceAccountCredentialValidationResult>> ValidateCredentialAsync(
    string apiKey,
    CancellationToken cancellationToken = default)
  {
    try
    {
      var parts = apiKey.Split(':', 2);
      if (parts.Length != 2)
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Invalid service account API key format.");
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
        return Result.Fail<ServiceAccountCredentialValidationResult>("Invalid service account API key format.");
      }

      if (credentialId == Guid.Empty)
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Invalid service account API key format.");
      }

      var credential = await appDb.ServiceAccountCredentials
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == credentialId, cancellationToken);

      if (credential is null)
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Invalid service account credential.");
      }

      var account = await appDb.ServiceAccounts
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == credential.ServiceAccountId, cancellationToken);

      if (account is null || !account.IsEnabled)
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Service account is not available.");
      }

      if (credential.RevokedAt is not null)
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Service account credential has been revoked.");
      }

      if (credential.ExpiresAt is not null && credential.ExpiresAt <= timeProvider.GetUtcNow())
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Service account credential has expired.");
      }

      var verification = passwordHasher.VerifyHashedPassword(string.Empty, credential.HashedSecret, parts[1]);
      if (verification == PasswordVerificationResult.Failed)
      {
        return Result.Fail<ServiceAccountCredentialValidationResult>("Invalid service account credential.");
      }

      // Update last-used timestamp. Rehash on the off chance the hasher signals an upgrade.
      if (verification == PasswordVerificationResult.SuccessRehashNeeded)
      {
        credential.HashedSecret = passwordHasher.HashPassword(string.Empty, parts[1]);
      }

      credential.LastUsedAt = timeProvider.GetUtcNow();
      await appDb.SaveChangesAsync(cancellationToken);

      return Result.Ok(new ServiceAccountCredentialValidationResult(account, credential));
    }
    catch (Exception ex)
    {
      return Result.Fail<ServiceAccountCredentialValidationResult>(ex, "Failed to validate service account credential.");
    }
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
        .Select(MapCredentialToDto)
        .ToList());
  }
}
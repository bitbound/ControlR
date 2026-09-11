using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services.ServiceAccounts;

/// <summary>
/// Manages service accounts and their credentials: bootstrap from configuration, CRUD for
/// server- and tenant-scoped accounts, credential creation/revocation and validation.
/// </summary>
public interface IServiceAccountManager
{
  /// <summary>
  /// Adds a credential to a server service account; the plaintext secret is only exposed
  /// this once. A null <paramref name="expiresAt"/> creates a credential that never expires.
  /// </summary>
  Task<HttpResult<CreateServiceAccountCredentialResult>> AddCredentialForServer(
    Guid serviceAccountId, string name, DateTimeOffset? expiresAt, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Adds a credential to a tenant-scoped service account. A null <paramref name="expiresAt"/>
  /// creates a credential that never expires.
  /// </summary>
  Task<HttpResult<CreateServiceAccountCredentialResult>> AddCredentialForTenant(
    Guid serviceAccountId, Guid tenantId, string name, DateTimeOffset? expiresAt, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Creates the bootstrapped server service account and its initial credential when the
  /// bootstrap options are fully supplied. Skips creation when the account already exists;
  /// throws when the bootstrap input is only partially configured.
  /// </summary>
  Task<HttpResult> BootstrapServerServiceAccount(CancellationToken cancellationToken);

  /// <summary>
  /// Creates a server service account with no credential; issue one via <see cref="AddCredentialForServer"/>.
  /// The access mode is required explicitly and never inferred. The <paramref name="actor"/>, when
  /// supplied, is recorded on the audit entry with its own principal type so that a service-account
  /// caller is not attributed as a human user.
  /// </summary>
  Task<HttpResult<ServiceAccountResult>> CreateForServer(
    string name, string? description, ServiceAccountAccessMode accessMode,
    CancellationToken cancellationToken = default, PrincipalDescriptor? actor = null);

  /// <summary>
  /// Creates a tenant-scoped service account with no credential; issue one via <see cref="AddCredentialForTenant"/>.
  /// </summary>
  Task<HttpResult<ServiceAccountResult>> CreateForTenant(
    string name, string? description, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a server service account; credentials cascade and orphaned PermissionAssignment
  /// rows for this account are removed.
  /// </summary>
  Task<HttpResult> DeleteForServer(Guid serviceAccountId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a tenant-scoped service account and its orphaned PermissionAssignment rows.
  /// </summary>
  Task<HttpResult> DeleteForTenant(Guid serviceAccountId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Returns all server-scoped service accounts with their credential metadata.
  /// </summary>
  Task<IReadOnlyList<ServiceAccountResult>> GetAllForServer(CancellationToken cancellationToken);

  /// <summary>
  /// Returns all tenant-scoped service accounts for a given tenant.
  /// </summary>
  Task<IReadOnlyList<ServiceAccountResult>> GetAllForTenant(Guid tenantId, CancellationToken cancellationToken);

  /// <summary>
  /// Returns a single server service account with its credential metadata.
  /// </summary>
  Task<HttpResult<ServiceAccountResult>> GetForServer(Guid serviceAccountId, CancellationToken cancellationToken);

  /// <summary>
  /// Returns a single tenant-scoped service account with its credential metadata.
  /// </summary>
  Task<HttpResult<ServiceAccountResult>> GetForTenant(Guid serviceAccountId, Guid tenantId, CancellationToken cancellationToken);

  /// <summary>
  /// Permanently deletes a credential on a server-scoped service account. Only credentials that
  /// are already revoked or expired can be deleted; active credentials must be revoked first.
  /// </summary>
  Task<HttpResult> PurgeCredentialForServer(
    Guid serviceAccountId, Guid credentialId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Permanently deletes a credential on a tenant-scoped service account. Only credentials that
  /// are already revoked or expired can be deleted; active credentials must be revoked first.
  /// </summary>
  Task<HttpResult> PurgeCredentialForTenant(
    Guid serviceAccountId, Guid credentialId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Revokes a credential on a server-scoped service account.
  /// </summary>
  Task<HttpResult> RevokeCredentialForServer(
    Guid serviceAccountId, Guid credentialId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Revokes a credential on a tenant-scoped service account.
  /// </summary>
  Task<HttpResult> RevokeCredentialForTenant(
    Guid serviceAccountId, Guid credentialId, Guid tenantId, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Updates a server service account's name, description, and enabled state.
  /// </summary>
  Task<HttpResult<ServiceAccountResult>> UpdateForServer(
    Guid serviceAccountId, string name, string? description, bool isEnabled, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Updates a tenant-scoped service account's name, description, and enabled state.
  /// </summary>
  Task<HttpResult<ServiceAccountResult>> UpdateForTenant(
    Guid serviceAccountId, Guid tenantId, string name, string? description, bool isEnabled, PrincipalDescriptor actor, CancellationToken cancellationToken);

  /// <summary>
  /// Validates a <c>{hex_id}:{plaintext_secret}</c> API key against a service account credential,
  /// updating <see cref="ServiceAccountCredential.LastUsedAt"/> on success. Revoked, expired,
  /// disabled-account, and invalid credentials all fail.
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
  IAuthorizationChangeLogFactory changeLogFactory,
  ILogger<ServiceAccountManager> logger) : IServiceAccountManager
{
  private const string InvalidApiKeyFormatMessage = "Invalid service account API key format.";
  private const string InvalidCredentialMessage = "Invalid service account credential.";
  private const int MinimumSecretLength = 32;

  private static readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);

  private readonly IAuthorizationChangeLogFactory _changeLogFactory = changeLogFactory;

  public async Task<HttpResult<CreateServiceAccountCredentialResult>> AddCredentialForServer(
    Guid serviceAccountId,
    string name,
    DateTimeOffset? expiresAt,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.BadRequest, "Credential name is required.");
    }

    if (!ValidateExpiration(expiresAt, out var expirationError))
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.BadRequest, expirationError);
    }

    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    if (!account.IsEnabled)
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.Forbidden, "Service account is disabled.");
    }

    var plainTextSecret = RandomGenerator.CreateApiKey();
    var hashedSecret = passwordHasher.HashPassword(string.Empty, plainTextSecret);

    var credential = new ServiceAccountCredential
    {
      Name = name,
      HashedSecret = hashedSecret,
      ExpiresAt = expiresAt
    };
    account.Credentials.Add(credential);

    await appDb.SaveChangesAsync(cancellationToken);

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountCredentialCreated,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccountCredential,
      credential.Id,
      null,
      after: new ServiceAccountCredentialSnapshot(name, serviceAccountId)));

    await appDb.SaveChangesAsync(cancellationToken);

    var apiKey = FormatApiKey(credential.Id, plainTextSecret);
    return HttpResult.Ok(new CreateServiceAccountCredentialResult(MapCredentialToResult(credential), apiKey));
  }

  public async Task<HttpResult<CreateServiceAccountCredentialResult>> AddCredentialForTenant(
    Guid serviceAccountId,
    Guid tenantId,
    string name,
    DateTimeOffset? expiresAt,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.BadRequest, "Credential name is required.");
    }

    if (!ValidateExpiration(expiresAt, out var expirationError))
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.BadRequest, expirationError);
    }

    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.NotFound, "Service account not found.");
    }

    if (!account.IsEnabled)
    {
      return HttpResult.Fail<CreateServiceAccountCredentialResult>(HttpResultErrorCode.Forbidden, "Service account is disabled.");
    }

    var plainTextSecret = RandomGenerator.CreateApiKey();
    var hashedSecret = passwordHasher.HashPassword(string.Empty, plainTextSecret);

    var credential = new ServiceAccountCredential
    {
      Name = name,
      HashedSecret = hashedSecret,
      ExpiresAt = expiresAt
    };
    account.Credentials.Add(credential);

    await appDb.SaveChangesAsync(cancellationToken);

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountCredentialCreated,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccountCredential,
      credential.Id,
      tenantId,
      after: new ServiceAccountCredentialSnapshot(name, serviceAccountId)));

    await appDb.SaveChangesAsync(cancellationToken);

    var apiKey = FormatApiKey(credential.Id, plainTextSecret);
    return HttpResult.Ok(new CreateServiceAccountCredentialResult(MapCredentialToResult(credential), apiKey));
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
      IsEnabled = true,
      AccessMode = ServiceAccountAccessMode.Unrestricted
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

  public async Task<HttpResult<ServiceAccountResult>> CreateForServer(
    string name,
    string? description,
    ServiceAccountAccessMode accessMode,
    CancellationToken cancellationToken = default,
    PrincipalDescriptor? actor = null)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    if (!Enum.IsDefined(accessMode))
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.BadRequest, "AccessMode is not a valid value.");
    }

    var nameConflict = await appDb.ServiceAccounts
      .AnyAsync(x => x.Kind == ServiceAccountKind.Server && x.Name == name, cancellationToken);
    if (nameConflict)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.Conflict, "A server service account with that name already exists.");
    }

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Server,
      TenantId = null,
      Name = name,
      Description = description,
      IsEnabled = true,
      AccessMode = accessMode
    };

    appDb.ServiceAccounts.Add(account);

    try
    {
      await appDb.ExecuteInTransaction(async () =>
      {
        await appDb.SaveChangesAsync(cancellationToken);

        appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
          AuthorizationChangeLogActions.ServiceAccountCreated,
          actor,
          AuthorizationChangeLogTargetTypes.ServiceAccount,
          account.Id,
          null,
          after: new ServiceAccountSnapshot(name, ServiceAccountKind.Server, description, true)));

        await appDb.SaveChangesAsync(cancellationToken);
      }, cancellationToken);
    }
    catch (DbUpdateException)
    {
      var conflictExists = await appDb.ServiceAccounts
        .IgnoreQueryFilters()
        .AnyAsync(x => x.Kind == ServiceAccountKind.Server && x.Name == name, cancellationToken);

      if (!conflictExists)
      {
        throw;
      }

      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.Conflict, "A server service account with that name already exists.");
    }

    return HttpResult.Ok(MapToResult(account));
  }

  public async Task<HttpResult<ServiceAccountResult>> CreateForTenant(
    string name,
    string? description,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var nameConflict = await appDb.ServiceAccounts
      .AnyAsync(x => x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId && x.Name == name, cancellationToken);
    if (nameConflict)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.Conflict, "A service account with that name already exists in this tenant.");
    }

    var account = new ServiceAccount
    {
      Kind = ServiceAccountKind.Tenant,
      TenantId = tenantId,
      Name = name,
      Description = description,
      IsEnabled = true
    };

    appDb.ServiceAccounts.Add(account);

    var saveResult = await appDb.SaveChangesOrConfirmConflictAsync<ServiceAccount>(
      x => x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId && x.Name == name,
      cancellationToken);

    if (saveResult == SaveChangesResult.ConflictDetected)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.Conflict, "A service account with that name already exists in this tenant.");
    }

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountCreated,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccount,
      account.Id,
      tenantId,
      after: new ServiceAccountSnapshot(name, ServiceAccountKind.Tenant, description, true)));

    await appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok(MapToResult(account));
  }

  public async Task<HttpResult> DeleteForServer(Guid serviceAccountId, PrincipalDescriptor actor, CancellationToken cancellationToken)
  {
    if (serviceAccountId.Equals(actor.PrincipalId))
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden, "A service account cannot delete itself.");
    }

    var account = await appDb.ServiceAccounts
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    await EvictAccountFromCache(serviceAccountId, cancellationToken);

    // Cascade: remove PermissionAssignment rows where this service account is the principal.
    var principalAssignments = await appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.ServiceAccount && x.PrincipalId == serviceAccountId)
      .ToListAsync(cancellationToken);

    appDb.PermissionAssignments.RemoveRange(principalAssignments);

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccount,
      serviceAccountId,
      null,
      before: new ServiceAccountSnapshot(account.Name, ServiceAccountKind.Server, account.Description, account.IsEnabled)));

    appDb.ServiceAccounts.Remove(account);
    await appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<HttpResult> DeleteForTenant(
    Guid serviceAccountId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (serviceAccountId.Equals(actor.PrincipalId))
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden, "A service account cannot delete itself.");
    }

    var account = await appDb.ServiceAccounts
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Service account not found.");
    }

    await EvictAccountFromCache(serviceAccountId, cancellationToken);

    // Cascade: remove PermissionAssignment rows where this service account is the principal.
    var principalAssignments = await appDb.PermissionAssignments
      .IgnoreQueryFilters()
      .Where(x => x.PrincipalKind == PermissionPrincipalKind.ServiceAccount && x.PrincipalId == serviceAccountId)
      .ToListAsync(cancellationToken);

    appDb.PermissionAssignments.RemoveRange(principalAssignments);

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccount,
      serviceAccountId,
      tenantId,
      before: new ServiceAccountSnapshot(account.Name, ServiceAccountKind.Tenant, account.Description, account.IsEnabled)));

    appDb.ServiceAccounts.Remove(account);
    await appDb.SaveChangesAsync(cancellationToken);

    return HttpResult.Ok();
  }

  public async Task<IReadOnlyList<ServiceAccountResult>> GetAllForServer(CancellationToken cancellationToken)
  {
    var accounts = await appDb.ServiceAccounts
      .Where(x => x.Kind == ServiceAccountKind.Server)
      .Include(x => x.Credentials)
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return [.. accounts.Select(MapToResult)];
  }

  public async Task<IReadOnlyList<ServiceAccountResult>> GetAllForTenant(Guid tenantId, CancellationToken cancellationToken)
  {
    var accounts = await appDb.ServiceAccounts
      .Where(x => x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId)
      .Include(x => x.Credentials)
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync(cancellationToken);

    return [.. accounts.Select(MapToResult)];
  }

  public async Task<HttpResult<ServiceAccountResult>> GetForServer(
    Guid serviceAccountId,
    CancellationToken cancellationToken)
  {
    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    return HttpResult.Ok(MapToResult(account));
  }

  public async Task<HttpResult<ServiceAccountResult>> GetForTenant(
    Guid serviceAccountId,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.NotFound, "Service account not found.");
    }

    return HttpResult.Ok(MapToResult(account));
  }

  public async Task<HttpResult> PurgeCredentialForServer(
    Guid serviceAccountId,
    Guid credentialId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(
        x => x.Id == credentialId &&
             x.ServiceAccountId == serviceAccountId &&
             x.ServiceAccount!.Kind == ServiceAccountKind.Server,
        cancellationToken);

    return await PurgeCredentialAsync(credential, serviceAccountId, owningTenantId: null, actor, cancellationToken);
  }

  public async Task<HttpResult> PurgeCredentialForTenant(
    Guid serviceAccountId,
    Guid credentialId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(
        x => x.Id == credentialId &&
             x.ServiceAccountId == serviceAccountId &&
             x.ServiceAccount!.Kind == ServiceAccountKind.Tenant &&
             x.ServiceAccount.TenantId == tenantId,
        cancellationToken);

    return await PurgeCredentialAsync(credential, serviceAccountId, tenantId, actor, cancellationToken);
  }

  public async Task<HttpResult> RevokeCredentialForServer(
    Guid serviceAccountId,
    Guid credentialId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    var credential = await appDb.ServiceAccountCredentials
      .FirstOrDefaultAsync(
        x => x.Id == credentialId &&
             x.ServiceAccountId == serviceAccountId &&
             x.ServiceAccount!.Kind == ServiceAccountKind.Server,
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

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountCredentialRevoked,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccountCredential,
      credentialId,
      null,
      before: new ServiceAccountCredentialSnapshot(credential.Name, serviceAccountId)));

    await appDb.SaveChangesAsync(cancellationToken);

    EvictCredentialFromCache(credentialId);
    return HttpResult.Ok();
  }

  public async Task<HttpResult> RevokeCredentialForTenant(
    Guid serviceAccountId,
    Guid credentialId,
    Guid tenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    var credential = await appDb.ServiceAccountCredentials
      .Include(x => x.ServiceAccount)
      .FirstOrDefaultAsync(
        x => x.Id == credentialId &&
             x.ServiceAccountId == serviceAccountId &&
             x.ServiceAccount!.Kind == ServiceAccountKind.Tenant &&
             x.ServiceAccount.TenantId == tenantId,
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

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountCredentialRevoked,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccountCredential,
      credentialId,
      tenantId,
      before: new ServiceAccountCredentialSnapshot(credential.Name, serviceAccountId)));

    await appDb.SaveChangesAsync(cancellationToken);

    EvictCredentialFromCache(credentialId);
    return HttpResult.Ok();
  }

  public async Task<HttpResult<ServiceAccountResult>> UpdateForServer(
    Guid serviceAccountId,
    string name,
    string? description,
    bool isEnabled,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Server, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.NotFound, "Server service account not found.");
    }

    var before = new ServiceAccountSnapshot(account.Name, ServiceAccountKind.Server, account.Description, account.IsEnabled);

    var isEnabledChanged = before.IsEnabled != isEnabled;

    account.Name = name;
    account.Description = description;
    account.IsEnabled = isEnabled;

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountUpdated,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccount,
      serviceAccountId,
      null,
      before: before,
      after: new ServiceAccountSnapshot(name, ServiceAccountKind.Server, description, isEnabled)));

    await appDb.SaveChangesAsync(cancellationToken);

    if (isEnabledChanged)
    {
      await EvictAccountFromCache(serviceAccountId, cancellationToken);
    }

    return HttpResult.Ok(MapToResult(account));
  }

  public async Task<HttpResult<ServiceAccountResult>> UpdateForTenant(
    Guid serviceAccountId,
    Guid tenantId,
    string name,
    string? description,
    bool isEnabled,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.BadRequest, "Name is required.");
    }

    var account = await appDb.ServiceAccounts
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Id == serviceAccountId && x.Kind == ServiceAccountKind.Tenant && x.TenantId == tenantId, cancellationToken);

    if (account is null)
    {
      return HttpResult.Fail<ServiceAccountResult>(HttpResultErrorCode.NotFound, "Service account not found.");
    }

    var before = new ServiceAccountSnapshot(account.Name, ServiceAccountKind.Tenant, account.Description, account.IsEnabled);

    var isEnabledChanged = before.IsEnabled != isEnabled;

    account.Name = name;
    account.Description = description;
    account.IsEnabled = isEnabled;

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountUpdated,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccount,
      serviceAccountId,
      tenantId,
      before: before,
      after: new ServiceAccountSnapshot(name, ServiceAccountKind.Tenant, description, isEnabled)));

    await appDb.SaveChangesAsync(cancellationToken);

    if (isEnabledChanged)
    {
      await EvictAccountFromCache(serviceAccountId, cancellationToken);
    }

    return HttpResult.Ok(MapToResult(account));
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
        await PersistLastUsedAt(credentialId, now, cancellationToken);

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

  private static ServiceAccountCredentialResult MapCredentialToResult(ServiceAccountCredential credential)
  {
    return new ServiceAccountCredentialResult(
      credential.Id,
      credential.Name,
      credential.CreatedAt,
      credential.ExpiresAt,
      credential.RevokedAt,
      credential.LastUsedAt);
  }

  private static ServiceAccountResult MapToResult(ServiceAccount account)
  {
    return new ServiceAccountResult(
      account.Id,
      account.Name,
      account.Description,
      account.Kind,
      account.IsEnabled,
      account.AccessMode,
      account.CreatedAt,
      account.Credentials
        .OrderBy(c => c.CreatedAt)
        .ThenBy(c => c.Id)
        .Select(MapCredentialToResult)
        .ToList());
  }

  private async Task EvictAccountFromCache(Guid serviceAccountId, CancellationToken cancellationToken)
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

  private async Task PersistLastUsedAt(Guid credentialId, DateTimeOffset now, CancellationToken cancellationToken)
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

  private async Task<HttpResult> PurgeCredentialAsync(
    ServiceAccountCredential? credential,
    Guid serviceAccountId,
    Guid? owningTenantId,
    PrincipalDescriptor actor,
    CancellationToken cancellationToken)
  {
    if (credential is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Credential not found.");
    }

    var now = timeProvider.GetUtcNow();
    var isDead = credential.RevokedAt is not null || (credential.ExpiresAt is not null && credential.ExpiresAt <= now);

    if (!isDead)
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, "Only revoked or expired credentials can be deleted. Revoke the credential first.");
    }

    appDb.AuthorizationChangeLogs.Add(_changeLogFactory.Create(
      AuthorizationChangeLogActions.ServiceAccountCredentialDeleted,
      actor,
      AuthorizationChangeLogTargetTypes.ServiceAccountCredential,
      credential.Id,
      owningTenantId,
      before: new ServiceAccountCredentialSnapshot(credential.Name, serviceAccountId)));

    appDb.ServiceAccountCredentials.Remove(credential);
    await appDb.SaveChangesAsync(cancellationToken);

    EvictCredentialFromCache(credential.Id);
    return HttpResult.Ok();
  }

  private bool ValidateExpiration(DateTimeOffset? expiresAt, out string error)
  {
    error = string.Empty;
    if (expiresAt is null)
    {
      return true;
    }

    if (expiresAt <= timeProvider.GetUtcNow())
    {
      error = "Credential expiration must be in the future.";
      return false;
    }

    return true;
  }
}

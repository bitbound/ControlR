using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services.AgentInstaller;

public interface IAgentInstallerKeyManager
{
  Task<CreateInstallerKeyResponseDto> CreateKey(
      Guid tenantId,
      Guid creatorId,
      CreatorKind creatorKind,
      InstallerKeyType keyType,
      uint? allowedUses,
      DateTimeOffset? expiration,
      string? friendlyName);
  Task<HttpResult> DeleteKey(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin);
  Task<IReadOnlyList<AgentInstallerKeyDto>> GetAllKeys(Guid tenantId, Guid userId, bool isTenantAdmin);
  Task<HttpResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetKeyUsages(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin);
  Task<HttpResult> RenameKey(Guid keyId, string friendlyName, Guid userId, Guid tenantId, bool isTenantAdmin);
  Task<HttpResult<AgentInstallerKey>> TryGetKey(Guid keyId, Guid tenantId);
  /// <summary>
  /// Validates the key, consumes a usage if it is valid, and returns the key.
  /// Use this as the final step when creating/updating a device, not for non-consuming checks.
  /// </summary>
  Task<HttpResult<AgentInstallerKey>> ValidateAndConsumeKey(Guid keyId, string keySecret, Guid deviceId, string? remoteIpAddress = null);
  /// <summary>
  /// Validates the key without consuming a usage. Returns the key if valid.
  /// Use this when you need to check key validity before performing other operations.
  /// </summary>
  Task<HttpResult<AgentInstallerKey>> ValidateKey(Guid keyId, string keySecret);
}

public class AgentInstallerKeyManager(
    TimeProvider timeProvider,
    IDbContextFactory<AppDb> dbContextFactory,
    IPasswordHasher<string> passwordHasher,
    IOptions<AppOptions> appOptions,
    ILogger<AgentInstallerKeyManager> logger) : IAgentInstallerKeyManager
{
  private readonly IOptions<AppOptions> _appOptions = appOptions;
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<AgentInstallerKeyManager> _logger = logger;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<CreateInstallerKeyResponseDto> CreateKey(
      Guid tenantId,
      Guid creatorId,
      CreatorKind creatorKind,
      InstallerKeyType keyType,
      uint? allowedUses,
      DateTimeOffset? expiration,
      string? friendlyName)
  {
    var plaintextKey = RandomGenerator.CreateAccessToken();
    var hashedKey = _passwordHasher.HashPassword(string.Empty, plaintextKey);

    var effectiveExpiration = keyType == InstallerKeyType.UsageBased
      ? _timeProvider.GetUtcNow() + TimeSpan.FromHours(24)
      : expiration;

    var installerKey = new AgentInstallerKey
    {
      TenantId = tenantId,
      CreatorId = creatorId,
      CreatorKind = creatorKind,
      HashedKey = hashedKey,
      KeyType = keyType,
      AllowedUses = allowedUses,
      Expiration = effectiveExpiration,
      FriendlyName = friendlyName
    };

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    db.AgentInstallerKeys.Add(installerKey);
    await db.SaveChangesAsync();

    return installerKey.ToInternalResponseDto(plaintextKey);
  }

  public async Task<HttpResult> DeleteKey(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var key = await db.AgentInstallerKeys
        .FirstOrDefaultAsync(x => x.Id == keyId && x.TenantId == tenantId);

    if (key is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Key not found");
    }

    if (!isTenantAdmin && key.CreatorId != userId)
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden, "Permission denied");
    }

    db.AgentInstallerKeys.Remove(key);
    await db.SaveChangesAsync();

    return HttpResult.Ok();
  }

  public async Task<IReadOnlyList<AgentInstallerKeyDto>> GetAllKeys(Guid tenantId, Guid userId, bool isTenantAdmin)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();

    var query = db.AgentInstallerKeys
        .AsNoTracking()
        .Where(x => x.TenantId == tenantId);

    if (!isTenantAdmin)
    {
      query = query.Where(x => x.CreatorId == userId);
    }

    var now = _timeProvider.GetUtcNow();
    var cutoff = GetUsageHistoryCutoff();

    query = query.Where(x =>
        x.KeyType == InstallerKeyType.Persistent ||
        (x.Expiration.HasValue && x.Expiration.Value >= now));

    var keyData = await query
        .Select(x => new
        {
            x.Id,
            x.CreatorId,
          CreatorName = db.Users
            .Where(u => u.Id == x.CreatorId)
            .Select(u => u.UserName)
            .FirstOrDefault(),
            x.KeyType,
            x.CreatedAt,
            x.AllowedUses,
            x.Expiration,
            x.FriendlyName,
            UsageCount = db.AgentInstallerKeyUsages.Count(u =>
                u.AgentInstallerKeyId == x.Id &&
                u.TenantId == x.TenantId &&
                (!cutoff.HasValue || u.CreatedAt >= cutoff.Value))
        })
        .Where(x =>
          x.KeyType != InstallerKeyType.UsageBased ||
          (x.AllowedUses.HasValue && x.UsageCount < x.AllowedUses.Value))
        .ToListAsync();

      return keyData.Select(x => new AgentInstallerKeyDto(
        x.Id,
        x.CreatorId,
        x.CreatorName,
        x.KeyType,
        x.CreatedAt,
        x.AllowedUses,
        x.Expiration,
        x.FriendlyName,
        x.UsageCount)).ToList();
  }

  public async Task<HttpResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetKeyUsages(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var key = await db.AgentInstallerKeys
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == keyId && x.TenantId == tenantId);

    if (key is null)
    {
      return HttpResult.Fail<IReadOnlyList<AgentInstallerKeyUsageDto>>(HttpResultErrorCode.NotFound, "Key not found");
    }

    if (!isTenantAdmin && key.CreatorId != userId)
    {
      return HttpResult.Fail<IReadOnlyList<AgentInstallerKeyUsageDto>>(HttpResultErrorCode.Forbidden, "Permission denied");
    }

    var cutoff = GetUsageHistoryCutoff();

    var usages = await db.AgentInstallerKeyUsages
        .AsNoTracking()
        .Where(x => x.AgentInstallerKeyId == keyId && x.TenantId == tenantId)
        .Where(x => !cutoff.HasValue || x.CreatedAt >= cutoff.Value)
        .Select(x => new AgentInstallerKeyUsageDto(x.Id, x.DeviceId, x.CreatedAt, x.RemoteIpAddress))
      .ToListAsync();

    return HttpResult.Ok<IReadOnlyList<AgentInstallerKeyUsageDto>>(usages);
  }

  public async Task<HttpResult> RenameKey(Guid keyId, string friendlyName, Guid userId, Guid tenantId, bool isTenantAdmin)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var key = await db.AgentInstallerKeys
        .FirstOrDefaultAsync(x => x.Id == keyId && x.TenantId == tenantId);

    if (key is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Key not found");
    }

    if (!isTenantAdmin && key.CreatorId != userId)
    {
      return HttpResult.Fail(HttpResultErrorCode.Forbidden, "Permission denied");
    }

    key.FriendlyName = friendlyName;
    await db.SaveChangesAsync();

    return HttpResult.Ok();
  }

  public async Task<HttpResult<AgentInstallerKey>> TryGetKey(Guid keyId, Guid tenantId)
  {
    if (keyId == Guid.Empty)
    {
      return HttpResult.Fail<AgentInstallerKey>(HttpResultErrorCode.BadRequest, "Key ID is empty");
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var storedKey = await db.AgentInstallerKeys
        .FirstOrDefaultAsync(x => x.Id == keyId && x.TenantId == tenantId);

    if (storedKey is null)
    {
      return HttpResult.Fail<AgentInstallerKey>(HttpResultErrorCode.NotFound, "Key not found");
    }

    return HttpResult.Ok(storedKey);
  }

  public async Task<HttpResult<AgentInstallerKey>> ValidateAndConsumeKey(
    Guid keyId,
    string keySecret,
    Guid deviceId,
    string? remoteIpAddress = null)
  {
    var result = await ValidateKeyImpl(keyId, keySecret, consumeUsage: true, deviceId, remoteIpAddress);
    if (!result.IsSuccess)
    {
      _logger.LogError("Installer key validation and consume failed.  Key ID: {KeyId}", keyId);
    }

    return result;
  }

  public async Task<HttpResult<AgentInstallerKey>> ValidateKey(Guid keyId, string keySecret)
  {
    var result = await ValidateKeyImpl(keyId, keySecret, consumeUsage: false, deviceId: null, remoteIpAddress: null);
    if (!result.IsSuccess)
    {
      _logger.LogError("Installer key validation failed.  Key ID: {KeyId}", keyId);
    }

    return result;
  }

  private static bool IsExpired(
    InstallerKeyType keyType,
    DateTimeOffset? expiration,
    uint? allowedUses,
    int usageCount,
    DateTimeOffset now)
  {
    return keyType switch
    {
      InstallerKeyType.TimeBased => !expiration.HasValue || expiration.Value < now,
      InstallerKeyType.UsageBased => !expiration.HasValue || expiration.Value < now || usageCount >= allowedUses,
      _ => false
    };
  }

  private async Task<bool> AddUsageAndUpdateKey(
    AppDb db,
    AgentInstallerKey installerKey,
    int currentUsageCount,
    Guid? deviceId,
    string? remoteIpAddress)
  {
    db.AgentInstallerKeyUsages.Add(new AgentInstallerKeyUsage
    {
      AgentInstallerKeyId = installerKey.Id,
      CreatedAt = _timeProvider.GetUtcNow(),
      TenantId = installerKey.TenantId,
      DeviceId = deviceId ?? Guid.Empty,
      RemoteIpAddress = remoteIpAddress
    });
    if (installerKey.KeyType == InstallerKeyType.UsageBased && currentUsageCount + 1 >= installerKey.AllowedUses)
    {
      db.AgentInstallerKeys.Remove(installerKey);
      await db.SaveChangesAsync();
      return true;
    }


    await db.SaveChangesAsync();
    return false;
  }

  private DateTimeOffset? GetUsageHistoryCutoff()
  {
    var historyDays = _appOptions.Value.AgentInstallerKeyHistoryDays;
    if (historyDays <= 0)
    {
      return null;
    }

    return _timeProvider.GetUtcNow() - TimeSpan.FromDays(historyDays);
  }

  private async Task<HttpResult<AgentInstallerKey>> ValidateKeyImpl(
    Guid keyId,
    string keySecret,
    bool consumeUsage,
    Guid? deviceId,
    string? remoteIpAddress)
  {
    if (keyId == Guid.Empty)
    {
      return HttpResult.Fail<AgentInstallerKey>(HttpResultErrorCode.BadRequest, "Key ID is empty");
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var installerKey = await db.AgentInstallerKeys
      .FirstOrDefaultAsync(x => x.Id == keyId);

    if (installerKey is null)
    {
      return HttpResult.Fail<AgentInstallerKey>(HttpResultErrorCode.NotFound, "Key not found");
    }

    var hashResult = _passwordHasher.VerifyHashedPassword(string.Empty, installerKey.HashedKey, keySecret);
    if (hashResult != PasswordVerificationResult.Success)
    {
      return HttpResult.Fail<AgentInstallerKey>(HttpResultErrorCode.Unauthorized, "Invalid key secret");
    }

    var now = _timeProvider.GetUtcNow();

    var usageCount = 0;
    if (installerKey.KeyType == InstallerKeyType.UsageBased)
    {
      usageCount = await db.AgentInstallerKeyUsages
          .CountAsync(u => u.AgentInstallerKeyId == keyId);
    }

    var isValid = installerKey.KeyType switch
    {
      InstallerKeyType.Persistent => true,
      InstallerKeyType.UsageBased =>
        !IsExpired(installerKey.KeyType, installerKey.Expiration, installerKey.AllowedUses, usageCount, now) &&
        installerKey.Expiration.HasValue && installerKey.Expiration.Value >= now,
      InstallerKeyType.TimeBased => installerKey.Expiration.HasValue && installerKey.Expiration.Value >= now,
      _ => false
    };

    if (!isValid)
    {
      if (installerKey.KeyType == InstallerKeyType.TimeBased ||
          installerKey.KeyType == InstallerKeyType.UsageBased)
      {
        db.AgentInstallerKeys.Remove(installerKey);
        await db.SaveChangesAsync();
      }
      return HttpResult.Fail<AgentInstallerKey>(HttpResultErrorCode.BadRequest, "Key has expired or is otherwise invalid");
    }

    if (consumeUsage)
    {
      _ = await AddUsageAndUpdateKey(db, installerKey, usageCount, deviceId, remoteIpAddress);
    }

    return HttpResult.Ok(installerKey);
  }
}
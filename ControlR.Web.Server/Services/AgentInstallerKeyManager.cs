using ControlR.Libraries.Shared.Helpers;
using ControlR.Web.Server.Extensions;
using ControlR.Web.Server.Primitives;

namespace ControlR.Web.Server.Services;

public interface IAgentInstallerKeyManager
{
  Task<CreateInstallerKeyResponseDto> CreateKey(
      Guid tenantId,
      Guid creatorId,
      InstallerKeyType keyType,
      uint? allowedUses,
      DateTimeOffset? expiration,
      string? friendlyName);
  Task<HttpResult> DeleteKey(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin);
  Task<IReadOnlyList<AgentInstallerKeyDto>> GetAllKeys(Guid tenantId, Guid userId, bool isTenantAdmin);
  Task<HttpResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetKeyUsages(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin);
  Task<HttpResult> IncrementUsage(Guid keyId, Guid? deviceId = null, string? remoteIpAddress = null);
  Task<HttpResult> RenameKey(Guid keyId, string friendlyName, Guid userId, Guid tenantId, bool isTenantAdmin);
  Task<Result<AgentInstallerKey>> TryGetKey(Guid keyId);
  /// <summary>
  /// Validates the key and consumes a usage if valid. Use this as the final step when
  /// creating/updating a device.
  /// </summary>
  Task<bool> ValidateAndConsumeKey(Guid keyId, string keySecret, Guid deviceId, string? remoteIpAddress = null);
  /// <summary>
  /// Validates the key without consuming a usage. Use this when you need to check key validity
  /// before performing other operations.
  /// </summary>
  Task<bool> ValidateKey(Guid keyId, string keySecret);
}

public class AgentInstallerKeyManager(
    TimeProvider timeProvider,
    IDbContextFactory<AppDb> dbContextFactory,
    IPasswordHasher<string> passwordHasher,
    ILogger<AgentInstallerKeyManager> logger) : IAgentInstallerKeyManager
{
  private readonly IDbContextFactory<AppDb> _dbContextFactory = dbContextFactory;
  private readonly ILogger<AgentInstallerKeyManager> _logger = logger;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<CreateInstallerKeyResponseDto> CreateKey(
      Guid tenantId,
      Guid creatorId,
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
      HashedKey = hashedKey,
      KeyType = keyType,
      AllowedUses = allowedUses,
      Expiration = effectiveExpiration,
      FriendlyName = friendlyName
    };

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    db.AgentInstallerKeys.Add(installerKey);
    await db.SaveChangesAsync();

    return installerKey.ToCreateResponseDto(plaintextKey);
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
        .Include(x => x.Usages)
        .Where(x => x.TenantId == tenantId);

    if (!isTenantAdmin)
    {
      query = query.Where(x => x.CreatorId == userId);
    }

    var keys = await query.ToListAsync();
    var now = _timeProvider.GetUtcNow();

    var expiredKeys = keys.Where(k => IsExpired(k, now)).ToList();
    if (expiredKeys.Count > 0)
    {
      db.AgentInstallerKeys.RemoveRange(expiredKeys);
      await db.SaveChangesAsync();
    }

    return keys.Except(expiredKeys).Select(x => x.ToDto()).ToList();
  }

  public async Task<HttpResult<IReadOnlyList<AgentInstallerKeyUsageDto>>> GetKeyUsages(Guid keyId, Guid userId, Guid tenantId, bool isTenantAdmin)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var key = await db.AgentInstallerKeys
        .Include(x => x.Usages)
        .FirstOrDefaultAsync(x => x.Id == keyId && x.TenantId == tenantId);

    if (key is null)
    {
      return HttpResult.Fail<IReadOnlyList<AgentInstallerKeyUsageDto>>(HttpResultErrorCode.NotFound, "Key not found");
    }

    if (!isTenantAdmin && key.CreatorId != userId)
    {
      return HttpResult.Fail<IReadOnlyList<AgentInstallerKeyUsageDto>>(HttpResultErrorCode.Forbidden, "Permission denied");
    }

    var usages = key.Usages
        .Select(x => new AgentInstallerKeyUsageDto(x.Id, x.DeviceId, x.CreatedAt, x.RemoteIpAddress))
        .ToList();

    return HttpResult.Ok<IReadOnlyList<AgentInstallerKeyUsageDto>>(usages);
  }

  public async Task<HttpResult> IncrementUsage(Guid keyId, Guid? deviceId = null, string? remoteIpAddress = null)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var installerKey = await db.AgentInstallerKeys
        .Include(x => x.Usages)
        .FirstOrDefaultAsync(x => x.Id == keyId);

    if (installerKey is null)
    {
      return HttpResult.Fail(HttpResultErrorCode.NotFound, "Installer key not found");
    }

    var now = _timeProvider.GetUtcNow();

    if (installerKey.KeyType == InstallerKeyType.TimeBased ||
        installerKey.KeyType == InstallerKeyType.UsageBased)
    {
      var isExpired = !installerKey.Expiration.HasValue || installerKey.Expiration.Value < now;
      if (isExpired)
      {
        db.AgentInstallerKeys.Remove(installerKey);
        await db.SaveChangesAsync();
        return HttpResult.Fail(HttpResultErrorCode.BadRequest, "Key has expired");
      }
    }

    if (installerKey.KeyType == InstallerKeyType.UsageBased && installerKey.Usages.Count >= installerKey.AllowedUses)
    {
      return HttpResult.Fail(HttpResultErrorCode.BadRequest, "Key usage limit reached");
    }

    await AddUsageAndUpdateKey(db, installerKey, deviceId, remoteIpAddress);
    return HttpResult.Ok();
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

  public async Task<Result<AgentInstallerKey>> TryGetKey(Guid keyId)
  {
    if (keyId == Guid.Empty)
    {
      return Result.Fail<AgentInstallerKey>("Key ID is empty");
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var storedKey = await db.AgentInstallerKeys.FindAsync(keyId);

    if (storedKey is null)
    {
      return Result.Fail<AgentInstallerKey>("Key not found");
    }

    return Result.Ok(storedKey);
  }

  public async Task<bool> ValidateAndConsumeKey(
    Guid keyId,
    string keySecret,
    Guid deviceId,
    string? remoteIpAddress = null)
  {
    var isValid = await ValidateKeyImpl(keyId, keySecret, consumeUsage: true, deviceId, remoteIpAddress);
    if (!isValid)
    {
      _logger.LogError("Installer key validation and consume failed.  Key ID: {KeyId}", keyId);
    }

    return isValid;
  }

  public async Task<bool> ValidateKey(Guid keyId, string keySecret)
  {
    var isValid = await ValidateKeyImpl(keyId, keySecret, consumeUsage: false, deviceId: null, remoteIpAddress: null);
    if (!isValid)
    {
      _logger.LogError("Installer key validation failed.  Key ID: {KeyId}", keyId);
    }

    return isValid;
  }

  private static async Task AddUsageAndUpdateKey(
    AppDb db,
    AgentInstallerKey installerKey,
    Guid? deviceId,
    string? remoteIpAddress)
  {
    installerKey.Usages.Add(new AgentInstallerKeyUsage()
    {
      TenantId = installerKey.TenantId,
      DeviceId = deviceId ?? Guid.Empty,
      RemoteIpAddress = remoteIpAddress
    });

    if (installerKey.KeyType == InstallerKeyType.UsageBased && installerKey.Usages.Count >= installerKey.AllowedUses)
    {
      db.AgentInstallerKeys.Remove(installerKey);
    }
    else
    {
      db.AgentInstallerKeys.Update(installerKey);
    }

    await db.SaveChangesAsync();
  }

  private static bool IsExpired(AgentInstallerKey key, DateTimeOffset now)
  {
    return key.KeyType switch
    {
      InstallerKeyType.TimeBased => !key.Expiration.HasValue || key.Expiration.Value < now,
      InstallerKeyType.UsageBased => !key.Expiration.HasValue || key.Expiration.Value < now || key.Usages.Count >= key.AllowedUses,
      _ => false
    };
  }

  private async Task<bool> ValidateKeyImpl(
    Guid keyId,
    string keySecret,
    bool consumeUsage,
    Guid? deviceId,
    string? remoteIpAddress)
  {
    if (keyId == Guid.Empty)
    {
      return false;
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var installerKey = await db.AgentInstallerKeys
      .Include(x => x.Usages)
      .FirstOrDefaultAsync(x => x.Id == keyId);

    if (installerKey is null)
    {
      return false;
    }

    var hashResult = _passwordHasher.VerifyHashedPassword(string.Empty, installerKey.HashedKey, keySecret);
    if (hashResult != PasswordVerificationResult.Success)
    {
      return false;
    }

    var now = _timeProvider.GetUtcNow();
    var isValid = installerKey.KeyType switch
    {
      InstallerKeyType.Persistent => true,
      InstallerKeyType.UsageBased =>
        installerKey.Usages.Count < installerKey.AllowedUses &&
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
      return false;
    }

    if (consumeUsage)
    {
      await AddUsageAndUpdateKey(db, installerKey, deviceId, remoteIpAddress);
    }

    return true;
  }
}
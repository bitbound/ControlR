using ControlR.Libraries.Shared.Helpers;

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

  Task<Result> IncrementUsage(Guid keyId, Guid? deviceId = null, string? remoteIpAddress = null);
  Task<Result<AgentInstallerKey>> TryGetKey(string key, Guid? keyId = null);
  Task<bool> ValidateKey(string key, Guid? keyId = null, Guid? deviceId = null, string? remoteIpAddress = null);
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

    var installerKey = new AgentInstallerKey
    {
      TenantId = tenantId,
      CreatorId = creatorId,
      HashedKey = hashedKey,
      KeyType = keyType,
      AllowedUses = allowedUses,
      Expiration = expiration,
      FriendlyName = friendlyName
    };

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    db.AgentInstallerKeys.Add(installerKey);
    await db.SaveChangesAsync();

    return installerKey.ToCreateResponseDto(plaintextKey);
  }

  public async Task<Result> IncrementUsage(Guid keyId, Guid? deviceId = null, string? remoteIpAddress = null)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var installerKey = await db.AgentInstallerKeys
        .Include(x => x.Usages)
        .FirstOrDefaultAsync(x => x.Id == keyId);

    if (installerKey is null)
    {
      return Result.Fail("Installer key not found");
    }

    if (installerKey.KeyType == InstallerKeyType.TimeBased)
    {
      var isExpired = !installerKey.Expiration.HasValue || installerKey.Expiration.Value < _timeProvider.GetUtcNow();
      if (isExpired)
      {
        db.AgentInstallerKeys.Remove(installerKey);
        await db.SaveChangesAsync();
        return Result.Fail("Key has expired");
      }
    }

    if (installerKey.KeyType == InstallerKeyType.UsageBased && installerKey.Usages.Count >= installerKey.AllowedUses)
    {
      return Result.Fail("Key usage limit reached");
    }

    await AddUsageAndUpdateKey(db, installerKey, deviceId, remoteIpAddress);
    return Result.Ok();
  }

  public async Task<Result<AgentInstallerKey>> TryGetKey(string key, Guid? keyId = null)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync();

    if (keyId.HasValue)
    {
      var storedKey = await db.AgentInstallerKeys.FindAsync(keyId.Value);
      if (storedKey is not null)
      {
        var result = _passwordHasher.VerifyHashedPassword(string.Empty, storedKey.HashedKey, key);
        if (result == PasswordVerificationResult.Success)
        {
          return Result.Ok(storedKey);
        }
      }
      return Result.Fail<AgentInstallerKey>("Key not found or invalid");
    }

    var keys = await db.AgentInstallerKeys.ToListAsync();

    foreach (var storedKey in keys)
    {
      var result = _passwordHasher.VerifyHashedPassword(string.Empty, storedKey.HashedKey, key);
      if (result == PasswordVerificationResult.Success)
      {
        return Result.Ok(storedKey);
      }
    }

    return Result.Fail<AgentInstallerKey>("Key not found");
  }

  public async Task<bool> ValidateKey(string keySecret, Guid? keyId = null, Guid? deviceId = null, string? remoteIpAddress = null)
  {
    var isValid = await ValidateKeyImpl(keySecret, keyId, deviceId, remoteIpAddress);
    if (!isValid)
    {
      _logger.LogError("Installer key validation failed.  Key Secret: {KeySecret}", keySecret);
    }

    return isValid;
  }

  private async Task AddUsageAndUpdateKey(
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

  private async Task<bool> ValidateKeyImpl(string key, Guid? keyId, Guid? deviceId, string? remoteIpAddress)
  {
    var result = await TryGetKey(key, keyId);
    if (!result.IsSuccess)
    {
      return false;
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync();
    var installerKey = await db.AgentInstallerKeys
        .Include(x => x.Usages)
        .FirstOrDefaultAsync(x => x.Id == result.Value.Id);

    if (installerKey is null)
    {
      return false;
    }

    var isValid = installerKey.KeyType switch
    {
      InstallerKeyType.Persistent => true,
      InstallerKeyType.UsageBased => installerKey.Usages.Count < installerKey.AllowedUses,
      InstallerKeyType.TimeBased => installerKey.Expiration.HasValue && installerKey.Expiration.Value >= _timeProvider.GetUtcNow(),
      _ => false
    };

    if (!isValid)
    {
      if (installerKey.KeyType == InstallerKeyType.TimeBased)
      {
        db.AgentInstallerKeys.Remove(installerKey);
        await db.SaveChangesAsync();
      }
      return false;
    }

    await AddUsageAndUpdateKey(db, installerKey, deviceId, remoteIpAddress);
    return true;
  }
}
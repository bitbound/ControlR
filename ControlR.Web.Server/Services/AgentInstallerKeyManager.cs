using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services;

public interface IAgentInstallerKeyManager
{
  Task<AgentInstallerKey> CreateKey(Guid tenantId, Guid creatorId, InstallerKeyType keyType, uint? allowedUses, DateTimeOffset? expiration);
  Task<bool> ValidateKey(string key);
}

public class AgentInstallerKeyManager(
  TimeProvider timeProvider,
  ILogger<AgentInstallerKeyManager> logger) : IAgentInstallerKeyManager
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly ILogger<AgentInstallerKeyManager> _logger = logger;

  // We can use a HybridCache here later, if we keep this.  Installer
  // keys will probably go into the database, though, with a management
  // UI for them.
  private readonly MemoryCache _keyCache = new(new MemoryCacheOptions());

  public Task<AgentInstallerKey> CreateKey(
    Guid tenantId,
    Guid creatorId,
    InstallerKeyType keyType,
    uint? allowedUses,
    DateTimeOffset? expiration)
  {
    var key = RandomGenerator.CreateAccessToken();
    var installerKey = new AgentInstallerKey(tenantId, creatorId, key, keyType, allowedUses, expiration);

    switch (keyType)
    {
      case InstallerKeyType.UsageBased:
        {
          _keyCache.Set(key, installerKey);
          break;
        }
      case InstallerKeyType.TimeBased:
        {
          if (!expiration.HasValue)
          {
            throw new ArgumentNullException(nameof(expiration));
          }
          _keyCache.Set(key, installerKey, expiration.Value);
          break;
        }
      case InstallerKeyType.Unknown:
      default:
        throw new ArgumentOutOfRangeException(nameof(keyType), "Unknown installer key type.");
    }

    return installerKey.AsTaskResult();
  }

  public async Task<bool> ValidateKey(string key)
  {
    var isValid = await ValidateKeyImpl(key);
    if (!isValid)
    {
      _logger.LogError("Installer key validation failed.  Key: {Key}", key);
    }
    return isValid;
  }

  private Task<bool> ValidateKeyImpl(string key)
  {
    if (!_keyCache.TryGetValue(key, out var cachedObject))
    {
      return false.AsTaskResult();
    }

    if (cachedObject is not AgentInstallerKey installerKey)
    {
      return false.AsTaskResult();
    }

    switch (installerKey.KeyType)
    {
      case InstallerKeyType.Unknown:
        break;
      case InstallerKeyType.UsageBased:
        {
          // This operation has a race condition if multiple validations are being
          // performed on the same key concurrently.  But the risk/impact is so
          // small that it's not worth locking the resource.

          var isValid = installerKey.CurrentUses < installerKey.AllowedUses;
          installerKey = installerKey with { CurrentUses = installerKey.CurrentUses + 1 };

          if (installerKey.CurrentUses >= installerKey.AllowedUses)
          {
            _keyCache.Remove(key);
          }

          return isValid.AsTaskResult();
        }
      case InstallerKeyType.TimeBased:
        {
          var isValid = installerKey.Expiration.HasValue && installerKey.Expiration.Value >= _timeProvider.GetUtcNow();
          if (!isValid)
          {
            _keyCache.Remove(key);
          }
          return isValid.AsTaskResult();
        }
      default:
        _logger.LogError("Unknown installer key type: {KeyType}", installerKey.KeyType);
        break;
    }

    return false.AsTaskResult();
  }
}
using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services;

public interface IAgentInstallerKeyManager
{
  Task<AgentInstallerKey> CreateKey(Guid tenantId, Guid creatorId, InstallerKeyType keyType, uint? allowedUses,
    DateTimeOffset? expiration);

  bool TryGetKey(string key, [NotNullWhen(true)] out AgentInstallerKey? installerKey);
  Task<bool> ValidateKey(string key);
}

public class AgentInstallerKeyManager(
  TimeProvider timeProvider,
  ILogger<AgentInstallerKeyManager> logger) : IAgentInstallerKeyManager
{
  // We can use a HybridCache here later, if we keep this.  Installer
  // keys will probably go into the database, though, with a management
  // UI for them.
  private readonly MemoryCache _keyCache = new(new MemoryCacheOptions());
  private readonly ILogger<AgentInstallerKeyManager> _logger = logger;
  private readonly TimeProvider _timeProvider = timeProvider;

  public Task<AgentInstallerKey> CreateKey(
    Guid tenantId,
    Guid creatorId,
    InstallerKeyType keyType,
    uint? allowedUses,
    DateTimeOffset? expiration)
  {
    var keySecret = RandomGenerator.CreateAccessToken();
    var installerKey = new AgentInstallerKey(tenantId, creatorId, keySecret, keyType, allowedUses, expiration);

    switch (keyType)
    {
      case InstallerKeyType.UsageBased:
        {
          _keyCache.Set(keySecret, installerKey);
          break;
        }
      case InstallerKeyType.TimeBased:
        {
          if (!expiration.HasValue)
          {
            throw new ArgumentNullException(nameof(expiration));
          }

          _keyCache.Set(keySecret, installerKey, expiration.Value);
          break;
        }
      case InstallerKeyType.Unknown:
      default:
        throw new ArgumentOutOfRangeException(nameof(keyType), "Unknown installer key type.");
    }

    return installerKey.AsTaskResult();
  }

  public bool TryGetKey(string key, [NotNullWhen(true)] out AgentInstallerKey? installerKey) =>
    _keyCache.TryGetValue(key, out installerKey);

  public async Task<bool> ValidateKey(string keySecret)
  {
    var isValid = await ValidateKeyImpl(keySecret);
    if (!isValid)
    {
      _logger.LogError("Installer key validation failed.  Key Secret: {KeySecret}", keySecret);
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
          else
          {
            _keyCache.Set(key, installerKey);
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
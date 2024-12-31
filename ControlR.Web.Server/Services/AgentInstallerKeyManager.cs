using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services;

public interface IAgentInstallerKeyManager
{
  Task<AgentInstallerKey> CreateKey(Guid tenantId, Guid creatorId, InstallerKeyType keyType, DateTimeOffset? expiration);
  Task<bool> ValidateKey(string token);
}

public class AgentInstallerKeyManager(
  TimeProvider timeProvider,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<AgentInstallerKeyManager> logger) : IAgentInstallerKeyManager
{
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly ILogger<AgentInstallerKeyManager> _logger = logger;

  // We can use a HybridCache here later, if we keep this.  Installer
  // keys will probably go into the database, though, with a management
  // UI for them.
  private readonly MemoryCache _keyCache = new(new MemoryCacheOptions());

  public Task<AgentInstallerKey> CreateKey(
    Guid tenantId,
    Guid creatorId,
    InstallerKeyType keyType,
    DateTimeOffset? expiration)
  {
    var token = RandomGenerator.CreateAccessToken();
    var installerKey = new AgentInstallerKey(tenantId, creatorId, token, keyType, expiration);
    if (expiration.HasValue)
    {
      _keyCache.Set(token, installerKey, expiration.Value);
    }
    else
    {
      _keyCache.Set(token, installerKey);
    }
    return installerKey.AsTaskResult();
  }

  public Task<bool> ValidateKey(string token)
  {
    if (!_keyCache.TryGetValue(token, out var cachedObject))
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
      case InstallerKeyType.SingleUse:
        _keyCache.Remove(installerKey);
        return true.AsTaskResult();
      case InstallerKeyType.AbsoluteExpiration:
        var isValid = installerKey.Expiration.HasValue && installerKey.Expiration.Value >= _timeProvider.GetUtcNow();
        return isValid.AsTaskResult();
      default:
        _logger.LogError("Unknown installer key type: {KeyType}", installerKey.KeyType);
        break;
    }

    return false.AsTaskResult();
  }
}
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace ControlR.Web.Server.Services;

public interface IAgentInstallerKeyManager
{
  Task<AgentInstallerKey> CreateKey(Guid tenantId, Guid creatorId);
  Task<bool> ValidateKey(Guid tenantId, Guid creatorId, string token);
}

public class AgentInstallerKeyManager(IOptionsMonitor<AppOptions> appOptions) : IAgentInstallerKeyManager
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;

  // We can use a HybridCache here later, if we keep this.  Installer
  // keys will probably go into the database, though, with a management
  // UI for them.
  private readonly MemoryCache _keyCache = new(new MemoryCacheOptions());

  public Task<AgentInstallerKey> CreateKey(Guid tenantId, Guid creatorId)
  {
    var token = RandomGenerator.CreateAccessToken();
    var installerKey = new AgentInstallerKey(tenantId, creatorId, token);
    _keyCache.Set(token, installerKey, _appOptions.CurrentValue.AgentInstallerKeyExpiration);
    return installerKey.AsTaskResult();
  }

  public Task<bool> ValidateKey(Guid tenantId, Guid creatorId, string token)
  {
    var isValid = _keyCache.TryGetValue(token, out var cachedObject) &&
      cachedObject is AgentInstallerKey installerKey &&
      installerKey.TenantId == tenantId &&
      installerKey.CreatorId == creatorId;

    return isValid.AsTaskResult();
  }

  private MemoryCacheEntryOptions GetEntryOptions()
  {
    return new MemoryCacheEntryOptions()
    {
      SlidingExpiration = _appOptions.CurrentValue.AgentInstallerKeyExpiration
    };
  }
}
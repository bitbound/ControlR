using ControlR.Agent.Shared.Options;
using ControlR.Agent.Shared.Services;

namespace ControlR.Agent.LoadTester;

internal class FakeSettingsProvider(Guid deviceId, Uri serverUri) : IOptionsAccessor
{
  public Guid DeviceId => deviceId;

  public bool DisableAutoUpdate => true;
  public int HubDtoChunkSize => 100;
  public string InstanceId { get; } = string.Empty;

  public string? PrivateKey => null;

  public Uri ServerUri { get; } = serverUri;

  public Guid TenantId { get; } = Guid.NewGuid();

  public string GetAppSettingsPath()
  {
    return string.Empty;
  }

  public Guid GetRequiredTenantId()
  {
    return TenantId;
  }

  public Task UpdateAppOptions(AgentAppOptions options)
  {
    return Task.CompletedTask;
  }

  public Task UpdateId(Guid uid)
  {
    return Task.CompletedTask;
  }

  public Task UpdatePrivateKey(string privateKeyBase64)
  {
    return Task.CompletedTask;
  }
}

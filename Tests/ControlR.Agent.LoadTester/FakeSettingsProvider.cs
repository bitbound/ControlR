using ControlR.Agent.Common.Options;
using ControlR.Agent.Common.Services;
using ControlR.Libraries.Shared.Constants;

namespace ControlR.Agent.LoadTester;
internal class FakeSettingsProvider(Guid deviceId, Uri serverUri) : ISettingsProvider
{
  public Guid DeviceId => deviceId;
  public string InstanceId { get; } = string.Empty;

  public Uri ServerUri { get; } = serverUri;

  public int VncPort => AppConstants.DefaultVncPort;
  public int HubDtoChunkSize => 100;


  public bool DisableAutoUpdate => true;

  public Guid TenantId => default;

  public string GetAppSettingsPath()
  {
    return string.Empty;
  }

  public Task UpdateAppOptions(AgentAppOptions options)
  {
    return Task.CompletedTask;
  }

  public Task UpdateId(Guid uid)
  {
    return Task.CompletedTask;
  }
}

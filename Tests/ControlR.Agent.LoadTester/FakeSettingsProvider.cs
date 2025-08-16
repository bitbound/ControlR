using ControlR.Agent.Common.Services;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Agent.LoadTester;
internal class FakeSettingsProvider(Guid deviceId, Uri serverUri) : ISettingsProvider
{
  public Guid DeviceId => deviceId;
  public string InstanceId { get; } = string.Empty;

  public Uri ServerUri { get; } = serverUri;

  public int VncPort => AppConstants.DefaultVncPort;


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

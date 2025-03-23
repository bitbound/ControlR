using ControlR.Agent.Common.Services;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Agent.LoadTester;
internal class FakeSettingsProvider(Guid deviceId, Uri serverUri) : ISettingsProvider
{
  public Guid DeviceId => deviceId;
  public string InstanceId { get; } = string.Empty;

  public Uri ServerUri { get; } = serverUri;

  public string GetAppSettingsPath()
  {
    return string.Empty;
  }

  public Task UpdateId(Guid uid)
  {
    return Task.CompletedTask;
  }

  public Task UpdateSettings(AgentAppSettings settings)
  {
    return Task.CompletedTask;
  }
}

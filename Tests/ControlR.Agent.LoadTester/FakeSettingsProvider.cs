using ControlR.Agent.Common.Services;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Agent.LoadTester;
internal class FakeSettingsProvider(Guid deviceId, Uri serverUri) : ISettingsProvider
{
  public Guid DeviceId => deviceId;
  public string InstanceId { get; } = string.Empty;

  public bool IsConnectedToPublicServer => false;

  public Uri ServerUri { get; } = serverUri;

  public Task ClearTags()
  {
   return Task.CompletedTask;
  }

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

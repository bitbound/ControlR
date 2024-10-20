using System.Text.Json;
using ControlR.Libraries.Agent.Options;
using ControlR.Libraries.Agent.Startup;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Agent.Services;

internal interface ISettingsProvider
{
  Guid DeviceId { get; }
  bool IsConnectedToPublicServer { get; }
  Uri ServerUri { get; }
  string GetAppSettingsPath();
  Task UpdateSettings(AgentAppSettings settings);
  Task UpdateId(Guid uid);
}

internal class SettingsProvider(
  IOptionsMonitor<AgentAppOptions> _appOptions,
  IFileSystem _fileSystem,
  IOptions<InstanceOptions> _instanceOptions,
  ILogger<SettingsProvider> _logger) : ISettingsProvider
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly SemaphoreSlim _updateLock = new(1, 1);

  public Guid DeviceId => _appOptions.CurrentValue.DeviceId;

  public Uri ServerUri =>
    _appOptions.CurrentValue.ServerUri ??
    AppConstants.ServerUri ??
    throw new InvalidOperationException("Server URI is not configured correctly.");

  public bool IsConnectedToPublicServer =>
    _appOptions.CurrentValue.ServerUri?.Authority == AppConstants.ProdServerUri.Authority;

  public string GetAppSettingsPath()
  {
    return PathConstants.GetAppSettingsPath(_instanceOptions.Value.InstanceId);
  }

  public async Task UpdateSettings(AgentAppSettings settings)
  {
    await _updateLock.WaitAsync();
    try
    {
      await WriteToDisk(settings);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to update settings.");
    }
    finally
    {
      _updateLock.Release();
    }
  }

  public async Task UpdateId(Guid uid)
  {
    _appOptions.CurrentValue.DeviceId = uid;
    await WriteToDisk(_appOptions.CurrentValue);
  }

  private async Task WriteToDisk(AgentAppOptions options)
  {
    await WriteToDisk(new AgentAppSettings { AppOptions = options });
  }

  private async Task WriteToDisk(AgentAppSettings settings)
  {
    var content = JsonSerializer.Serialize(settings, _jsonOptions);
    await _fileSystem.WriteAllTextAsync(GetAppSettingsPath(), content);
  }
}
using System.Text.Json;
using ControlR.Libraries.Agent.Options;
using ControlR.Libraries.Agent.Startup;
using ControlR.Libraries.Shared.Constants;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Agent.Services;

internal interface ISettingsProvider
{
  Guid DeviceId { get; }
  string InstanceId { get; }
  Uri ServerUri { get; }

  /// <summary>
  /// Tags should only be set on the first successful connection, then cleared.
  /// </summary>
  Task ClearTags();

  string GetAppSettingsPath();
  Task UpdateId(Guid uid);
  Task UpdateSettings(AgentAppSettings settings);
}

internal class SettingsProvider(
  IOptionsMonitor<AgentAppOptions> appOptions,
  IFileSystem fileSystem,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<SettingsProvider> logger) : ISettingsProvider
{
  private readonly IOptionsMonitor<AgentAppOptions> _appOptions = appOptions;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly ILogger<SettingsProvider> _logger = logger;
  private readonly SemaphoreSlim _updateLock = new(1, 1);

  public Guid DeviceId => _appOptions.CurrentValue.DeviceId;

  public string InstanceId => _instanceOptions.Value.InstanceId ?? string.Empty;

  public Uri ServerUri =>
    _appOptions.CurrentValue.ServerUri ??
    AppConstants.ServerUri ??
    throw new InvalidOperationException("Server URI is not configured correctly.");

  public async Task ClearTags()
  {
    if (_appOptions.CurrentValue.TagIds is null)
    {
      return;
    }

    _appOptions.CurrentValue.TagIds = null;
    await WriteToDisk(_appOptions.CurrentValue);
  }

  public string GetAppSettingsPath()
  {
    return PathConstants.GetAppSettingsPath(_instanceOptions.Value.InstanceId);
  }

  public async Task UpdateId(Guid uid)
  {
    _appOptions.CurrentValue.DeviceId = uid;
    await WriteToDisk(_appOptions.CurrentValue);
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
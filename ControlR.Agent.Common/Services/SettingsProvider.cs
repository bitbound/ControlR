using System.Text.Json;
using System.Text.Json.Nodes;
using ControlR.Agent.Common.Startup;
using ControlR.Libraries.Shared.Constants;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services;

public interface ISettingsProvider
{
  Guid DeviceId { get; }
  bool DisableAutoUpdate { get; }
  string InstanceId { get; }
  Uri ServerUri { get; }
  Guid TenantId { get; }
  string GetAppSettingsPath();
  Guid GetRequiredTenantId();
  Task UpdateAppOptions(AgentAppOptions options);
  Task UpdateId(Guid uid);
}

internal class SettingsProvider(
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  IOptionsMonitor<AgentAppOptions> appOptions,
  IOptionsMonitor<DeveloperOptions> developerOptions,
  IOptions<InstanceOptions> instanceOptions) : ISettingsProvider
{
  private readonly IOptionsMonitor<AgentAppOptions> _appOptions = appOptions;
  private readonly IOptionsMonitor<DeveloperOptions> _developerOptions = developerOptions;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly SemaphoreSlim _updateLock = new(1, 1);

  public Guid DeviceId => _appOptions.CurrentValue.DeviceId;
  public bool DisableAutoUpdate => _developerOptions.CurrentValue.DisableAutoUpdate;
  public string InstanceId => _instanceOptions.Value.InstanceId ?? string.Empty;
  public Uri ServerUri =>
    _appOptions.CurrentValue.ServerUri ??
    AppConstants.ServerUri ??
    throw new InvalidOperationException("Server URI is not configured correctly.");
    
  public Guid TenantId => _appOptions.CurrentValue.TenantId;

  public string GetAppSettingsPath()
  {
    return _fileSystemPathProvider.GetAgentAppSettingsPath();
  }

  public Guid GetRequiredTenantId()
  {
    return _appOptions.CurrentValue.TenantId == default
    ? throw new InvalidOperationException("Tenant ID is not configured correctly.")
    : _appOptions.CurrentValue.TenantId;
  }

  public async Task UpdateAppOptions(AgentAppOptions options)
  {
    await _updateLock.WaitAsync();
    try
    {
      var path = GetAppSettingsPath();

      if (!_fileSystem.FileExists(path))
      {
        await _fileSystem.WriteAllTextAsync(path, "{}");
      }

      var contents = await _fileSystem.ReadAllTextAsync(path);
      var json = JsonNode.Parse(contents) ??
                 throw new InvalidOperationException("Failed to parse app settings JSON.");

      json[AgentAppOptions.SectionKey] = JsonSerializer.SerializeToNode(options);
      contents = JsonSerializer.Serialize(json, _jsonOptions);
      await _fileSystem.WriteAllTextAsync(path, contents);
    }
    finally
    {
      _updateLock.Release();
    }
  }

  public async Task UpdateId(Guid uid)
  {
    _appOptions.CurrentValue.DeviceId = uid;
    await UpdateAppOptions(_appOptions.CurrentValue);
  }
}
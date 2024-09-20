using System.Text.Json;
using ControlR.Agent.Options;
using ControlR.Agent.Startup;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services;

internal interface ISettingsProvider
{
  IReadOnlyList<AuthorizedKeyDto> AuthorizedKeys { get; }
  string DeviceId { get; }
  bool IsConnectedToPublicServer { get; }
  Uri ServerUri { get; }
  string GetAppSettingsPath();
  Task UpdatePublicKeyLabel(string publicKeyBase64, string publicKeyLabel);
  Task UpdateSettings(AgentAppSettings settings);
}

internal class SettingsProvider(
  IOptionsMonitor<AgentAppOptions> appOptions,
  IFileSystem fileSystem,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<SettingsProvider> logger) : ISettingsProvider
{
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
  private readonly SemaphoreSlim _updateLock = new(1, 1);


  public IReadOnlyList<AuthorizedKeyDto> AuthorizedKeys => appOptions.CurrentValue.AuthorizedKeys;


  public string DeviceId => appOptions.CurrentValue.DeviceId;

  public Uri ServerUri =>
    appOptions.CurrentValue.ServerUri ??
    AppConstants.ServerUri ??
    throw new InvalidOperationException("Server URI is not configured correctly.");

  public bool IsConnectedToPublicServer =>
    appOptions.CurrentValue.ServerUri?.Authority == AppConstants.ProdServerUri.Authority;

  public string GetAppSettingsPath()
  {
    return PathConstants.GetAppSettingsPath(instanceOptions.Value.InstanceId);
  }

  public async Task UpdatePublicKeyLabel(string publicKeyBase64, string publicKeyLabel)
  {
    await _updateLock.WaitAsync();
    try
    {
      var authorizedKeys = AuthorizedKeys.ToList();

      var index = authorizedKeys.FindIndex(x => x.PublicKey == publicKeyBase64);
      if (index == -1)
      {
        logger.LogWarning("Unable to update public key label.  Public key {PublicKey} not found.", publicKeyBase64);
        return;
      }

      authorizedKeys[index] = authorizedKeys[index] with { Label = publicKeyLabel };
      appOptions.CurrentValue.AuthorizedKeys = [.. authorizedKeys];
      await WriteToDisk(appOptions.CurrentValue);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to update public key label.");
    }
    finally
    {
      _updateLock.Release();
    }
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
      logger.LogError(ex, "Failed to update settings.");
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
    await fileSystem.WriteAllTextAsync(GetAppSettingsPath(), content);
  }
}
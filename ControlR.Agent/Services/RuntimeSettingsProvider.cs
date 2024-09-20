using System.Runtime.CompilerServices;
using System.Text.Json;
using ControlR.Agent.Options;
using ControlR.Agent.Startup;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Services;

public interface IRuntimeSettingsProvider
{
  Task<T?> TryGet<T>(Func<AgentRuntimeSettings, T?> getter, [CallerMemberName] string? caller = null);

  Task<AgentRuntimeSettings?> TryGetSettings();
  Task TrySet(Func<AgentRuntimeSettings, AgentRuntimeSettings> setter, [CallerMemberName] string? caller = null);
  Task TrySet(Action<AgentRuntimeSettings> setter, [CallerMemberName] string? caller = null);
}

public class RuntimeSettingsProvider(
  IFileSystem fileSystem,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<RuntimeSettingsProvider> logger) : IRuntimeSettingsProvider
{
  private readonly SemaphoreSlim _fileLock = new(1, 1);
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  public async Task<T?> TryGet<T>(Func<AgentRuntimeSettings, T?> getter, [CallerMemberName] string? caller = null)
  {
    if (!await _fileLock.WaitAsync(TimeSpan.FromSeconds(5)))
    {
      logger.LogError(
        "Timed out while attempting to get file lock for runtime settings. Caller: {CallerName}",
        caller);
      return default;
    }

    try
    {
      var filePath = PathConstants.GetRuntimeSettingsFilePath(instanceOptions.Value.InstanceId);
      if (!fileSystem.FileExists(filePath))
      {
        return default;
      }

      await using var fs = fileSystem.OpenFileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
      var deserialized = await JsonSerializer.DeserializeAsync<AgentRuntimeSettings>(fs);
      if (deserialized is not AgentRuntimeSettings settings)
      {
        return default;
      }

      return getter.Invoke(settings);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting runtime setting.");
      return default;
    }
    finally
    {
      _fileLock.Release();
    }
  }

  public async Task<AgentRuntimeSettings?> TryGetSettings()
  {
    return await TryGet(x => x);
  }

  public async Task TrySet(Func<AgentRuntimeSettings, AgentRuntimeSettings> setter,
    [CallerMemberName] string? caller = null)
  {
    if (!await _fileLock.WaitAsync(TimeSpan.FromSeconds(5)))
    {
      logger.LogError(
        "Timed out while attempting to get file lock for runtime settings. Caller: {CallerName}",
        caller);
      return;
    }

    try
    {
      var filePath = PathConstants.GetRuntimeSettingsFilePath(instanceOptions.Value.InstanceId);
      AgentRuntimeSettings? settings = null;

      if (fileSystem.FileExists(filePath))
      {
        await using var readStream =
          fileSystem.OpenFileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
          settings = await JsonSerializer.DeserializeAsync<AgentRuntimeSettings>(readStream) ??
                     new AgentRuntimeSettings();
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Error while deserializing settings.");
          settings = new AgentRuntimeSettings();
        }
      }
      else
      {
        settings = new AgentRuntimeSettings();
      }

      var newSettings = setter.Invoke(settings);

      await using var writeStream = fileSystem.OpenFileStream(filePath, FileMode.Create, FileAccess.Write);
      await JsonSerializer.SerializeAsync(writeStream, newSettings, _jsonOptions);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting runtime setting.");
    }
    finally
    {
      _fileLock.Release();
    }
  }

  public async Task TrySet(Action<AgentRuntimeSettings> setter, [CallerMemberName] string? caller = null)
  {
    await TrySet(x =>
      {
        setter.Invoke(x);
        return x;
      },
      caller);
  }
}
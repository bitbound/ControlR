using ControlR.Agent.Options;
using ControlR.Agent.Startup;
using ControlR.Devices.Common.Services;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ControlR.Agent.Services;

public interface IRuntimeSettingsProvider
{
    ManualResetEventAsync ServerProvidedSettingsSignal { get; }
    Task<bool?> GetGitHubEnabled();

    Task SetGitHubEnabled(bool isEnabled);

    Task<AgentRuntimeSettings?> TryGetSettings();
    Task TrySetSettings(AgentRuntimeSettings settings);
}
public class RuntimeSettingsProvider(
    IFileSystem _fileSystem,
    IOptions<InstanceOptions> _instanceOptions,
    ILogger<RuntimeSettingsProvider> _logger) : IRuntimeSettingsProvider
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    public ManualResetEventAsync ServerProvidedSettingsSignal { get; } = new();

    public async Task<bool?> GetGitHubEnabled()
    {
        return await TryGet(x => x.GitHubEnabled);
    }

    public async Task SetGitHubEnabled(bool isEnabled)
    {
        await TrySet(x => 
        {
            x.GitHubEnabled = isEnabled;
            return x;
        });
    }

    public async Task<AgentRuntimeSettings?> TryGetSettings()
    {
        return await TryGet(x => x);
    }

    public async Task TrySetSettings(AgentRuntimeSettings settings)
    {
        await TrySet(x => x = settings);
    }

    private async Task<T?> TryGet<T>(Func<AgentRuntimeSettings, T?> getter, [CallerMemberName] string? caller = null)
    {
        if (!await _fileLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            _logger.LogError(
                "Timed out while attempting to get file lock for runtime settings. Caller: {CallerName}",
                caller);
            return default;
        }

        try
        {
            var filePath = PathConstants.GetRuntimeSettingsFilePath(_instanceOptions.Value.InstanceId);
            if (!_fileSystem.FileExists(filePath))
            {
                return default;
            }

            using var fs = _fileSystem.OpenFileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            var deserialized = await JsonSerializer.DeserializeAsync<AgentRuntimeSettings>(fs);
            if (deserialized is not AgentRuntimeSettings settings)
            {
                return default;
            }

            return getter.Invoke(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting runtime setting.");
            return default;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task TrySet(Func<AgentRuntimeSettings, AgentRuntimeSettings> setter, [CallerMemberName] string? caller = null)
    {
        if (!await _fileLock.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            _logger.LogError(
                "Timed out while attempting to get file lock for runtime settings. Caller: {CallerName}",
                caller);
            return;
        }

        try
        {
            var filePath = PathConstants.GetRuntimeSettingsFilePath(_instanceOptions.Value.InstanceId);
            AgentRuntimeSettings? settings = null;

            if (_fileSystem.FileExists(filePath))
            {
                using var readStream = _fileSystem.OpenFileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = await JsonSerializer.DeserializeAsync<AgentRuntimeSettings>(readStream) ?? new();
            }
            else
            {
                settings = new();
            }

            var newSettings = setter.Invoke(settings);

            using var writeStream = _fileSystem.OpenFileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            await JsonSerializer.SerializeAsync(writeStream, newSettings, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting runtime setting.");
        }
        finally
        {
            _fileLock.Release();
        }
    }
}

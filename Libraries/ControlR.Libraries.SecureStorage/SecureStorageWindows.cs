using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// Windows implementation of secure storage using DPAPI (Data Protection API).
/// Data is encrypted using Windows DPAPI and stored in the user's AppData folder.
/// </summary>
[SupportedOSPlatform("windows")]
internal class SecureStorageWindows : ISecureStorage
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ILogger<SecureStorageWindows> _logger;
    private readonly SecureStorageOptions _options;
    private readonly string _storagePath;

    private bool _disposed;

    public SecureStorageWindows(ILogger<SecureStorageWindows> logger, IOptions<SecureStorageOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var controlRPath = Path.Combine(appDataPath, _options.ServiceName);
        Directory.CreateDirectory(controlRPath);
        _storagePath = Path.Combine(controlRPath, $"{_options.ServiceName.ToLowerInvariant()}_secure_storage.dat");
        _logger.LogInformation("Initialized Windows secure storage at {StoragePath}", _storagePath);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<string?> GetAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _fileLock.WaitAsync(cts.Token);
        try
        {
            _logger.LogDebug("Retrieving value for key: {Key} from {StoragePath}", key, _storagePath);
            var storage = await ReadStorageAsync();
            if (storage.TryGetValue(key, out var encryptedValue))
            {
                try
                {
                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedValue,
                        null,
                        DataProtectionScope.CurrentUser);
                    _logger.LogDebug("Successfully retrieved and decrypted value for key: {Key}", key);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogError(ex, "Failed to decrypt value for key: {Key}. The data may be corrupted or the user context has changed.", key);
                    throw new SecureStorageException($"Failed to decrypt value for key '{key}'. The data may be corrupted or the user context has changed.", ex);
                }
            }
            _logger.LogDebug("Key not found: {Key}", key);
            return null;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout waiting for file lock while retrieving key: {Key}", key);
            throw new SecureStorageException($"Timeout waiting for file lock while retrieving key '{key}'.", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RemoveAllAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _fileLock.WaitAsync(cts.Token);
        try
        {
            _logger.LogInformation("Removing all secure storage data from {StoragePath}", _storagePath);
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
                _logger.LogInformation("Successfully removed all secure storage data from {StoragePath}", _storagePath);
            }
            else
            {
                _logger.LogDebug("No secure storage file exists at {StoragePath}", _storagePath);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout waiting for file lock while removing all data");
            throw new SecureStorageException("Timeout waiting for file lock while removing all secure storage data.", ex);
        }
        catch (Exception ex) when (ex is not SecureStorageException)
        {
            _logger.LogError(ex, "Failed to remove all secure storage data from {StoragePath}", _storagePath);
            throw new SecureStorageException($"Failed to remove all secure storage data from '{_storagePath}'.", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _fileLock.WaitAsync(cts.Token);
        try
        {
            _logger.LogDebug("Attempting to remove key: {Key} from {StoragePath}", key, _storagePath);
            var storage = await ReadStorageAsync();
            var removed = storage.Remove(key);
            if (removed)
            {
                await WriteStorageAsync(storage);
                _logger.LogInformation("Successfully removed key: {Key}", key);
            }
            else
            {
                _logger.LogDebug("Key not found for removal: {Key}", key);
            }
            return removed;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout waiting for file lock while removing key: {Key}", key);
            throw new SecureStorageException($"Timeout waiting for file lock while removing key '{key}'.", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _fileLock.WaitAsync(cts.Token);
        try
        {
            _logger.LogDebug("Setting value for key: {Key} in {StoragePath}", key, _storagePath);
            var storage = await ReadStorageAsync();
            var valueBytes = Encoding.UTF8.GetBytes(value);
            try
            {
                var encryptedBytes = ProtectedData.Protect(
                    valueBytes,
                    null,
                    DataProtectionScope.CurrentUser);
                var isUpdate = storage.ContainsKey(key);
                storage[key] = encryptedBytes;
                await WriteStorageAsync(storage);
                _logger.LogInformation("{Action} key: {Key}", isUpdate ? "Updated" : "Added", key);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to encrypt value for key: {Key}", key);
                throw new SecureStorageException($"Failed to encrypt value for key '{key}'.", ex);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout waiting for file lock while setting key: {Key}", key);
            throw new SecureStorageException($"Timeout waiting for file lock while setting key '{key}'.", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _fileLock?.Dispose();
        }

        _disposed = true;
    }

    private async Task<Dictionary<string, byte[]>> ReadStorageAsync()
    {
        if (!File.Exists(_storagePath))
        {
            _logger.LogDebug("Storage file does not exist at {StoragePath}, returning empty dictionary", _storagePath);
            return new Dictionary<string, byte[]>();
        }

        try
        {
            _logger.LogDebug("Reading secure storage file from {StoragePath}", _storagePath);
            var json = await File.ReadAllTextAsync(_storagePath);
            var storage = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            
            var result = new Dictionary<string, byte[]>(storage.Count);
            foreach (var (key, base64Value) in storage)
            {
                result[key] = Convert.FromBase64String(base64Value);
            }
            
            _logger.LogDebug("Successfully read {Count} entries from secure storage", result.Count);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize secure storage file at {StoragePath}. The file may be corrupted.", _storagePath);
            throw new SecureStorageException($"Failed to deserialize secure storage file at '{_storagePath}'. The file may be corrupted.", ex);
        }
        catch (Exception ex) when (ex is not SecureStorageException)
        {
            _logger.LogError(ex, "Failed to read secure storage file from {StoragePath}", _storagePath);
            throw new SecureStorageException($"Failed to read secure storage file from '{_storagePath}'.", ex);
        }
    }

    private async Task WriteStorageAsync(Dictionary<string, byte[]> storage)
    {
        try
        {
            _logger.LogDebug("Writing {Count} entries to secure storage at {StoragePath}", storage.Count, _storagePath);
            
            var jsonStorage = new Dictionary<string, string>(storage.Count);
            foreach (var (key, value) in storage)
            {
                jsonStorage[key] = Convert.ToBase64String(value);
            }
            
            var json = JsonSerializer.Serialize(jsonStorage, _jsonOptions);
            await File.WriteAllTextAsync(_storagePath, json);
            
            _logger.LogDebug("Successfully wrote secure storage file to {StoragePath}", _storagePath);
        }
        catch (Exception ex) when (ex is not SecureStorageException)
        {
            _logger.LogError(ex, "Failed to write secure storage file to {StoragePath}", _storagePath);
            throw new SecureStorageException($"Failed to write secure storage file to '{_storagePath}'.", ex);
        }
    }
}

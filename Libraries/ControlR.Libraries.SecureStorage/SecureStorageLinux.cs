using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// Linux implementation of secure storage using file-based storage with AES encryption.
/// Data is encrypted using AES-GCM with a randomly generated key stored alongside the data
/// with restrictive permissions (600).
/// - Normal user: stores in ~/.config/{serviceName}/
/// - Root: stores in /etc/{serviceName}/
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
internal class SecureStorageLinux : ISecureStorage, IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly byte[] _encryptionKey;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly bool _isRoot;
    private readonly ILogger<SecureStorageLinux> _logger;
    private readonly SecureStorageOptions _options;
    private readonly string _storagePath;

    private bool _disposed;

    public SecureStorageLinux(ILogger<SecureStorageLinux> logger, IOptions<SecureStorageOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _isRoot = geteuid() == 0;

        string baseDirectory;
        if (_isRoot)
        {
            baseDirectory = Path.Combine("/etc", _options.ServiceName.ToLowerInvariant());
            _logger.LogInformation("Running as root, using system directory: {Directory}", baseDirectory);
        }
        else
        {
            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDirectory = Path.Combine(homePath, ".config", _options.ServiceName.ToLowerInvariant());
            _logger.LogInformation("Running as user, using config directory: {Directory}", baseDirectory);
        }

        Directory.CreateDirectory(baseDirectory);
        _storagePath = Path.Combine(baseDirectory, $"{_options.ServiceName.ToLowerInvariant()}_secure_storage");
        _encryptionKey = DeriveEncryptionKey();
        EnsureFilePermissions();
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
                    var decryptedBytes = Decrypt(encryptedValue);
                    _logger.LogDebug("Successfully retrieved and decrypted value for key: {Key}", key);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                catch (CryptographicException ex)
                {
                    _logger.LogError(ex, "Failed to decrypt value for key: {Key}. The data may be corrupted or the encryption key has changed.", key);
                    throw new SecureStorageException($"Failed to decrypt value for key '{key}'. The data may be corrupted or the encryption key has changed.", ex);
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
                var encryptedBytes = Encrypt(valueBytes);
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

    [DllImport("libc")]
    private static extern uint geteuid();

    private byte[] Decrypt(byte[] encryptedData)
    {
        // Use AES-GCM for authenticated encryption
        // Format: [12-byte nonce][16-byte authentication tag][ciphertext]
        const int nonceSize = 12;   // Recommended size for AES-GCM
        const int tagSize = 16;     // 128-bit authentication tag

        if (encryptedData.Length < nonceSize + tagSize)
        {
            throw new CryptographicException($"Encrypted data is too short ({encryptedData.Length} bytes). Expected at least {nonceSize + tagSize} bytes for nonce and tag.");
        }

        var nonce = new byte[nonceSize];
        var tag = new byte[tagSize];
        var ciphertext = new byte[encryptedData.Length - nonceSize - tagSize];

        Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(encryptedData, nonceSize, tag, 0, tagSize);
        Buffer.BlockCopy(encryptedData, nonceSize + tagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];

        using (var aesGcm = new AesGcm(_encryptionKey, tagSize))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return plaintext;
    }

    private byte[] DeriveEncryptionKey()
    {
        // Use a high-entropy key stored in a protected location
        // Generate and store the key on first use, or load existing key
        var keyPath = Path.Combine(Path.GetDirectoryName(_storagePath)!, ".encryption_key");
        
        byte[] key;
        if (File.Exists(keyPath))
        {
            try
            {
                key = File.ReadAllBytes(keyPath);
                if (key.Length != 32) // AES-256 requires 32 bytes
                {
                    _logger.LogWarning("Existing encryption key has invalid length, generating new key");
                    key = GenerateAndStoreKey(keyPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read encryption key, generating new key");
                key = GenerateAndStoreKey(keyPath);
            }
        }
        else
        {
            key = GenerateAndStoreKey(keyPath);
        }

        return key;
    }

    private byte[] Encrypt(byte[] plaintext)
    {
        // Use AES-GCM for authenticated encryption to protect confidentiality and integrity
        // Format: [12-byte nonce][16-byte authentication tag][ciphertext]
        const int nonceSize = 12;   // Recommended size for AES-GCM
        const int tagSize = 16;     // 128-bit authentication tag

        var nonce = RandomNumberGenerator.GetBytes(nonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[tagSize];

        using (var aesGcm = new AesGcm(_encryptionKey, tagSize))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Combine nonce, tag, and ciphertext into a single buffer
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    private void EnsureFilePermissions()
    {
        if (!File.Exists(_storagePath))
        {
            return;
        }

        SetFilePermissions(_storagePath);
    }

    private byte[] GenerateAndStoreKey(string keyPath)
    {
        // Generate a cryptographically secure random key
        var key = RandomNumberGenerator.GetBytes(32); // AES-256
        
        try
        {
            // Write key to file
            File.WriteAllBytes(keyPath, key);
            
            // Set restrictive permissions (600) on the key file immediately
            SetFilePermissions(keyPath);
            
            _logger.LogInformation("Generated new encryption key at {KeyPath}", keyPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store encryption key at {KeyPath}", keyPath);
            throw new SecureStorageException($"Failed to store encryption key at '{keyPath}'.", ex);
        }

        return key;
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
            var storage = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            
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

    private void SetFilePermissions(string filePath)
    {
        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set file permissions on {FilePath}", filePath);
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
            
            SetFilePermissions(_storagePath);
            _logger.LogDebug("Successfully wrote secure storage file to {StoragePath}", _storagePath);
        }
        catch (Exception ex) when (ex is not SecureStorageException)
        {
            _logger.LogError(ex, "Failed to write secure storage file to {StoragePath}", _storagePath);
            throw new SecureStorageException($"Failed to write secure storage file to '{_storagePath}'.", ex);
        }
    }
}
namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// Provides a simple key/value store for securely storing data across platforms.
/// Data is encrypted and stored using platform-specific secure storage mechanisms:
/// - Windows: DPAPI (Data Protection API)
/// - Linux: File-based storage with restrictive permissions
/// - macOS: Keychain Services
/// </summary>
public interface ISecureStorage : IDisposable
{
    /// <summary>
    /// Gets a value from secure storage for the given key.
    /// </summary>
    /// <param name="key">The storage key. Cannot be null, empty, or whitespace.</param>
    /// <returns>
    /// The value associated with the key, or null if the key does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="SecureStorageException">
    /// Thrown when an error occurs during retrieval, such as decryption failure or I/O errors.
    /// </exception>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Removes all values from secure storage.
    /// </summary>
    /// <exception cref="SecureStorageException">Thrown when an error occurs during removal.</exception>
    /// <remarks>
    /// On macOS, this operation may not be fully supported due to keychain limitations.
    /// Individual items must be enumerated and removed separately.
    /// </remarks>
    Task RemoveAllAsync();

    /// <summary>
    /// Removes a value from secure storage for the given key.
    /// </summary>
    /// <param name="key">The storage key to remove. Cannot be null, empty, or whitespace.</param>
    /// <returns>
    /// True if the key was found and successfully removed; false if the key does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="SecureStorageException">Thrown when an error occurs during removal.</exception>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    /// Sets a value in secure storage for the given key.
    /// If the key already exists, its value will be updated.
    /// </summary>
    /// <param name="key">The storage key. Cannot be null, empty, or whitespace.</param>
    /// <param name="value">The value to store. Cannot be null.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="SecureStorageException">
    /// Thrown when an error occurs during storage, such as encryption failure or I/O errors.
    /// </exception>
    Task SetAsync(string key, string value);
}

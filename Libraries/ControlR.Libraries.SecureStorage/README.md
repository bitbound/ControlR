# ControlR.Libraries.SecureStorage

A cross-platform secure storage library for ControlR, providing a simple key/value API similar to MAUI's SecureStorage.

## Features

- **Cross-platform**: Works on Windows, Linux, and macOS
- **Platform-specific security**: Uses the best secure storage mechanism for each platform
- **Simple API**: Similar to MAUI's SecureStorage API
- **No UI required**: Works in daemon/service contexts

## Platform-Specific Implementations

### Windows
- Uses **DPAPI (Data Protection API)** for encryption
- Stores encrypted data in `%LOCALAPPDATA%\ControlR\`
- Encryption is user-specific and tied to the Windows user account

### Linux
- Uses **AES-GCM encryption** for authenticated encryption that protects both confidentiality and integrity
- Encryption key is a randomly generated 256-bit key stored in a secure file with permissions `600`
- Stores encrypted data in `~/.config/controlr/` (user) or `/etc/controlr/` (root)
- File permissions are set to `600` (read/write for owner only) atomically during file creation

### macOS
- Uses **Keychain Services** via the Security framework
- Automatically selects the appropriate keychain:
  - **System keychain** (`/Library/Keychains/System.keychain`) when running as root (e.g., LaunchDaemons)
  - **User keychain** (default) when running as a regular user (e.g., LaunchAgents or user applications)
- Data is stored securely with appropriate access controls for the execution context
- Note: `RemoveAll()` is not supported on macOS (items must be removed individually)

## Usage

### Basic Usage

```csharp
using ControlR.Libraries.SecureStorage;

// Store a value
await SecureStorage.Default.SetAsync("api_token", "secret-token-value");

// Retrieve a value
var token = await SecureStorage.Default.GetAsync("api_token");
if (token == null)
{
    // No value found for this key
}

// Remove a value
bool removed = SecureStorage.Default.Remove("api_token");

// Remove all values (not supported on macOS)
SecureStorage.Default.RemoveAll();
```

### With Dependency Injection

```csharp
using ControlR.Libraries.SecureStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging();

// Register ISecureStorage with default options
services.AddSecureStorage();

// Or register ISecureStorage with custom options (e.g., custom service name)
services.AddSecureStorage(options =>
{
    options.ServiceName = "MyCustomApp";
});

var serviceProvider = services.BuildServiceProvider();

// Resolve ISecureStorage from the service provider
var secureStorage = serviceProvider.GetRequiredService<ISecureStorage>();

await secureStorage.SetAsync("key", "value");

// Alternatively, you can still create an instance manually if needed:
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

// Create with default options
ISecureStorage manualSecureStorage = SecureStorage.Create(loggerFactory);

// Or create with custom options
ISecureStorage customSecureStorage = SecureStorage.Create(
    loggerFactory, 
    options => options.ServiceName = "MyCustomApp");
```

### Custom Service Name

You can configure a custom service name to isolate storage for different applications or contexts:

```csharp
// Using dependency injection
services.AddSecureStorage(options =>
{
    options.ServiceName = "MyApp";
});

// Using Create method
var secureStorage = SecureStorage.Create(
    loggerFactory,
    options => options.ServiceName = "MyApp");
```

The service name affects storage location:
- **Windows**: Creates a subdirectory in `%LOCALAPPDATA%\{ServiceName}\`
- **Linux**: Creates a subdirectory in `~/.config/{servicename}/` (user) or `/etc/{servicename}/` (root)
- **macOS**: Uses the service name as the keychain service identifier
```

## API Reference

### ISecureStorage Interface

#### GetAsync
```csharp
Task<string?> GetAsync(string key)
```
Gets a value from secure storage for the given key. Returns `null` if not found.

**Throws:**
- `ArgumentException` - If key is null or whitespace
- `SecureStorageException` - If an error occurs during decryption or storage access

#### SetAsync
```csharp
Task SetAsync(string key, string value)
```
Sets a value in secure storage for the given key.

**Throws:**
- `ArgumentException` - If key is null or whitespace
- `ArgumentNullException` - If value is null
- `SecureStorageException` - If an error occurs during encryption or storage access

#### Remove
```csharp
bool Remove(string key)
```
Removes a value from secure storage for the given key. Returns `true` if the key was found and removed.

**Throws:**
- `ArgumentException` - If key is null or whitespace
- `SecureStorageException` - If an error occurs during storage access

#### RemoveAll
```csharp
void RemoveAll()
```
Removes all values from secure storage.

**Note:** Not supported on macOS - throws `NotSupportedException`.

**Throws:**
- `SecureStorageException` - If an error occurs during storage access
- `NotSupportedException` - On macOS

## Security Considerations

- **Windows**: Security is tied to the Windows user account. Data encrypted by one user cannot be decrypted by another.
- **Linux**: Security relies on file permissions and AES-GCM encryption with a randomly generated key. The encryption key is stored in a file with restrictive permissions (600) in the same directory as the secure storage. Data files are created with secure permissions atomically to prevent race conditions.
- **macOS**: Security is managed by the keychain system. The library automatically uses:
  - **System keychain** when running as root - appropriate for system services and LaunchDaemons
  - **User keychain** when running as a regular user - appropriate for user applications and LaunchAgents
  - Data is accessible only to the appropriate security context (system or user)
- **No UI**: This library is designed to work without UI interaction, making it suitable for services and daemons.
- **Best Practices**: Do not store highly sensitive data (like private keys) for extended periods. Use secure storage for temporary secrets like API tokens and session data.

## Testing

For testing purposes, you can use the `SecureStorage.Create()` method with a custom logger factory, or mock the `ISecureStorage` interface.

## License

This library is part of the ControlR project and follows the same license.

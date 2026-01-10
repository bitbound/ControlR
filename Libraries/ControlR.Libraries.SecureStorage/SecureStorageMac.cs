using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// macOS implementation of secure storage using Keychain Services.
/// Data is stored in the system keychain when running as root (suitable for LaunchDaemons),
/// or the user's keychain when running as a regular user.
/// </summary>
[SupportedOSPlatform("macos")]
internal class SecureStorageMac : ISecureStorage
{
  private const string SystemKeychainPath = "/Library/Keychains/System.keychain";

  private readonly IntPtr _keychain;
  private readonly ILogger<SecureStorageMac> _logger;
  private readonly SemaphoreSlim _operationLock = new(1, 1);
  private readonly SecureStorageOptions _options;

  private bool _disposed;

  public SecureStorageMac(ILogger<SecureStorageMac> logger, IOptions<SecureStorageOptions> options)
  {
    _logger = logger;
    _options = options.Value;

    var isRoot = geteuid() == 0;

    if (isRoot)
    {
      _logger.LogInformation("Running as root, attempting to use system keychain");
      var status = SecKeychainOpen(SystemKeychainPath, out _keychain);

      if (status != 0)
      {
        _logger.LogWarning("Failed to open system keychain, will use default keychain. Status: {Status}", status);
        _keychain = IntPtr.Zero;
      }
      else
      {
        _logger.LogInformation("Successfully opened system keychain");
      }
    }
    else
    {
      _logger.LogInformation("Running as regular user, using user keychain");
      _keychain = IntPtr.Zero;
    }
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
    await _operationLock.WaitAsync(cts.Token);
    try
    {
      _logger.LogDebug("Retrieving value for key: {Key} from keychain", key);
      var serviceBytes = Encoding.UTF8.GetBytes(_options.ServiceName);
      var accountBytes = Encoding.UTF8.GetBytes(key);

      var status = SecKeychainFindGenericPassword(
        _keychain,
        (uint)serviceBytes.Length,
        serviceBytes,
        (uint)accountBytes.Length,
        accountBytes,
        out var passwordLength,
        out var passwordData,
        out _);

      if (status == 0)
      {
        try
        {
          var passwordBytes = new byte[passwordLength];
          Marshal.Copy(passwordData, passwordBytes, 0, (int)passwordLength);
          _logger.LogDebug("Successfully retrieved value for key: {Key}", key);
          return Encoding.UTF8.GetString(passwordBytes);
        }
        finally
        {
          _ = SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
        }
      }
      else if (status == -25300)
      {
        _logger.LogDebug("Key not found: {Key}", key);
        return null;
      }
      else
      {
        _logger.LogError("Failed to get keychain item for key: {Key}, OSStatus: {Status}", key, status);
        throw new SecureStorageException($"Failed to get keychain item for key '{key}'. OSStatus: {status}");
      }
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogError(ex, "Timeout waiting for operation lock while retrieving key: {Key}", key);
      throw new SecureStorageException($"Timeout waiting for operation lock while retrieving key '{key}'.", ex);
    }
    finally
    {
      _operationLock.Release();
    }
  }

  public Task RemoveAllAsync()
  {
    _logger.LogWarning("RemoveAll is not fully supported on macOS keychain. Individual items must be enumerated and removed if needed.");
    return Task.CompletedTask;
  }

  public async Task<bool> RemoveAsync(string key)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(key);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await _operationLock.WaitAsync(cts.Token);
    try
    {
      _logger.LogDebug("Attempting to remove key: {Key} from keychain", key);
      var serviceBytes = Encoding.UTF8.GetBytes(_options.ServiceName);
      var accountBytes = Encoding.UTF8.GetBytes(key);

      var status = SecKeychainFindGenericPassword(
          _keychain,
          (uint)serviceBytes.Length,
          serviceBytes,
          (uint)accountBytes.Length,
          accountBytes,
          out _,
          out _,
          out var itemRef);

      if (status == 0)
      {
        try
        {
          var deleteStatus = SecKeychainItemDelete(itemRef);
          if (deleteStatus == 0)
          {
            _logger.LogInformation("Successfully removed key: {Key}", key);
            return true;
          }
          else
          {
            _logger.LogError("Failed to delete keychain item for key: {Key}, OSStatus: {Status}", key, deleteStatus);
            throw new SecureStorageException($"Failed to delete keychain item for key '{key}'. OSStatus: {deleteStatus}");
          }
        }
        finally
        {
          CFRelease(itemRef);
        }
      }
      else if (status == -25300)
      {
        _logger.LogDebug("Key not found for removal: {Key}", key);
        return false;
      }
      else
      {
        _logger.LogError("Failed to find keychain item for key: {Key}, OSStatus: {Status}", key, status);
        throw new SecureStorageException($"Failed to find keychain item for key '{key}'. OSStatus: {status}");
      }
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogError(ex, "Timeout waiting for operation lock while removing key: {Key}", key);
      throw new SecureStorageException($"Timeout waiting for operation lock while removing key '{key}'.", ex);
    }
    finally
    {
      _operationLock.Release();
    }
  }

  public async Task SetAsync(string key, string value)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(key);
    ArgumentNullException.ThrowIfNull(value);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await _operationLock.WaitAsync(cts.Token);
    try
    {
      _logger.LogDebug("Setting value for key: {Key} in keychain", key);
      var serviceBytes = Encoding.UTF8.GetBytes(_options.ServiceName);
      var accountBytes = Encoding.UTF8.GetBytes(key);
      var passwordBytes = Encoding.UTF8.GetBytes(value);

      var findStatus = SecKeychainFindGenericPassword(
        _keychain,
        (uint)serviceBytes.Length,
        serviceBytes,
        (uint)accountBytes.Length,
        accountBytes,
        out _,
        out _,
        out var itemRef);

      if (findStatus == 0)
      {
        try
        {
          var updateStatus = SecKeychainItemModifyAttributesAndData(
            itemRef,
            IntPtr.Zero,
            (uint)passwordBytes.Length,
            passwordBytes);

          if (updateStatus != 0)
          {
            _logger.LogError("Failed to update keychain item for key: {Key}, OSStatus: {Status}", key, updateStatus);
            throw new SecureStorageException($"Failed to update keychain item for key '{key}'. OSStatus: {updateStatus}");
          }
          _logger.LogInformation("Updated key: {Key}", key);
        }
        finally
        {
          CFRelease(itemRef);
        }
      }
      else if (findStatus == -25300)
      {
        var addStatus = SecKeychainAddGenericPassword(
          _keychain,
          (uint)serviceBytes.Length,
          serviceBytes,
          (uint)accountBytes.Length,
          accountBytes,
          (uint)passwordBytes.Length,
          passwordBytes,
          out var addedItemRef);

        if (addStatus != 0)
        {
          _logger.LogError("Failed to add keychain item for key: {Key}, OSStatus: {Status}", key, addStatus);
          throw new SecureStorageException($"Failed to add keychain item for key '{key}'. OSStatus: {addStatus}");
        }

        if (addedItemRef != IntPtr.Zero)
        {
          CFRelease(addedItemRef);
        }
        _logger.LogInformation("Added key: {Key}", key);
      }
      else
      {
        _logger.LogError("Failed to check keychain for key: {Key}, OSStatus: {Status}", key, findStatus);
        throw new SecureStorageException($"Failed to check keychain for key '{key}'. OSStatus: {findStatus}");
      }
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogError(ex, "Timeout waiting for operation lock while setting key: {Key}", key);
      throw new SecureStorageException($"Timeout waiting for operation lock while setting key '{key}'.", ex);
    }
    finally
    {
      _operationLock.Release();
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
      _operationLock?.Dispose();
    }

    _disposed = true;
  }

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern void CFRelease(IntPtr cf);

  [DllImport("/System/Library/Frameworks/Security.framework/Security")]
  private static extern int SecKeychainAddGenericPassword(
      IntPtr keychain,
      uint serviceNameLength,
      byte[] serviceName,
      uint accountNameLength,
      byte[] accountName,
      uint passwordLength,
      byte[] passwordData,
      out IntPtr itemRef);

  [DllImport("/System/Library/Frameworks/Security.framework/Security")]
  private static extern int SecKeychainFindGenericPassword(
      IntPtr keychain,
      uint serviceNameLength,
      byte[] serviceName,
      uint accountNameLength,
      byte[] accountName,
      out uint passwordLength,
      out IntPtr passwordData,
      out IntPtr itemRef);

  [DllImport("/System/Library/Frameworks/Security.framework/Security")]
  private static extern int SecKeychainItemDelete(IntPtr itemRef);

  [DllImport("/System/Library/Frameworks/Security.framework/Security")]
  private static extern int SecKeychainItemFreeContent(
      IntPtr attrList,
      IntPtr data);

  [DllImport("/System/Library/Frameworks/Security.framework/Security")]
  private static extern int SecKeychainItemModifyAttributesAndData(
      IntPtr itemRef,
      IntPtr attrList,
      uint length,
      byte[] data);

  [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Unicode)]
  private static extern int SecKeychainOpen(
      string pathName,
      out IntPtr keychain);

  [DllImport("libc")]
  private static extern uint geteuid();
}

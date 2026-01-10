using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// Provides access to the platform-specific secure storage implementation.
/// </summary>
public static class SecureStorage
{
  private static ISecureStorage? _default;

  /// <summary>
  /// Gets the default secure storage implementation for the current platform.
  /// </summary>
  public static ISecureStorage Default
  {
    get
    {
      _default ??= Create(NullLoggerFactory.Instance);
      return _default;
    }
  }

  /// <summary>
  /// Creates a new secure storage instance with the specified logger factory.
  /// </summary>
  /// <param name="loggerFactory">The logger factory to use for logging.</param>
  /// <param name="configure">Optional configuration action for secure storage options.</param>
  /// <returns>A platform-specific secure storage implementation.</returns>
  public static ISecureStorage Create(
    ILoggerFactory loggerFactory,
    Action<SecureStorageOptions>? configure = null)
  {
    var options = new SecureStorageOptions();
    configure?.Invoke(options);
    var wrappedOptions = Options.Create(options);

    if (OperatingSystem.IsWindows())
    {
      var logger = loggerFactory.CreateLogger<SecureStorageWindows>();
      return new SecureStorageWindows(logger, wrappedOptions);
    }
    else if (OperatingSystem.IsMacOS())
    {
      var logger = loggerFactory.CreateLogger<SecureStorageMac>();
      return new SecureStorageMac(logger, wrappedOptions);
    }
    else if (OperatingSystem.IsLinux())
    {
      var logger = loggerFactory.CreateLogger<SecureStorageLinux>();
      return new SecureStorageLinux(logger, wrappedOptions);
    }
    else
    {
      throw new PlatformNotSupportedException("Secure storage is not supported on this platform.");
    }
  }
}

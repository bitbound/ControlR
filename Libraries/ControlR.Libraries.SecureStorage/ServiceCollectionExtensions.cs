using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.SecureStorage;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds the appropriate ISecureStorage implementation based on the current platform.
  /// </summary>
  /// <param name="services">The service collection to add to.</param>
  /// <param name="configure">Optional configuration action for secure storage options.</param>
  /// <returns>The updated service collection.</returns>
  public static IServiceCollection AddSecureStorage(
    this IServiceCollection services,
    Action<SecureStorageOptions>? configure = null)
  {
    if (configure is not null)
    {
      services.Configure(configure);
    }

#pragma warning disable CA1416
    if (OperatingSystem.IsWindows())
    {
      services.AddSingleton<ISecureStorage, SecureStorageWindows>();
    }
    else if (OperatingSystem.IsLinux())
    {
      services.AddSingleton<ISecureStorage, SecureStorageLinux>();
    }
    else if (OperatingSystem.IsMacOS())
    {
      services.AddSingleton<ISecureStorage, SecureStorageMac>();
    }
    else
    {
      throw new PlatformNotSupportedException("Secure storage is not supported on this platform.");
    }
#pragma warning restore CA1416
    return services;
  }
}
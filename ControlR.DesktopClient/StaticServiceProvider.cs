using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.Serilog;
using ControlR.Libraries.Shared.Services;
using System.Collections.Immutable;

namespace ControlR.DesktopClient;

internal static class StaticServiceProvider
{
  private static ServiceProvider? _designTimeProvider;
  private static ServiceProvider? _provider;
  private static ImmutableList<ServiceDescriptor>? _serviceDescriptors;

  public static IServiceProvider Instance => _provider ?? GetDesignTimeProvider();

  public static void Build(
    IControlledApplicationLifetime lifetime,
    string? instanceId)
  {
    if (_provider is not null)
    {
      return;
    }

    var services = new ServiceCollection();
    services.AddSingleton(lifetime);
    services.AddControlrDesktop(instanceId);
    _serviceDescriptors = [.. services];
    _provider = services.BuildServiceProvider();
  }

  internal static ImmutableList<ServiceDescriptor> GetServiceDescriptors() => 
    _serviceDescriptors ?? throw new InvalidOperationException("Service provider has not been built yet.");

  private static IServiceCollection AddControlrDesktop(
    this IServiceCollection services,
    string? instanceId = null)
  {
    var configuration = new ConfigurationBuilder()
      .AddEnvironmentVariables()
      .Build();

    services.AddSingleton<IConfiguration>(configuration);

    services.AddLogging(builder =>
    {
      builder
        .AddConsole()
        .AddDebug();
    });

    if (!Design.IsDesignMode)
    {
      services.BootstrapSerilog(
        configuration,
        GetDesktopLogsPath(instanceId),
        TimeSpan.FromDays(7),
        config =>
        {
          if (SystemEnvironment.Instance.IsDebug)
          {
            config.MinimumLevel.Debug();
          }
        });
    }

    services
      .AddDesktopShellServices(instanceId)
      .AddDesktopAppPlatformServices();

    return services;
  }

  private static ServiceProvider GetDesignTimeProvider()
  {
    if (_designTimeProvider is not null)
    {
      return _designTimeProvider;
    }

    var services = new ServiceCollection();
    services.AddControlrDesktop();
    _designTimeProvider = services.BuildServiceProvider();
    return _designTimeProvider;
  }

  private static string GetDesktopLogsPath(string? instanceId)
  {
#if IS_WINDOWS
    return ControlR.DesktopClient.Windows.PathConstants.GetLogsPath(instanceId);
#elif IS_MACOS
    return ControlR.DesktopClient.Mac.PathConstants.GetLogsPath(instanceId);
#elif IS_LINUX
    return ControlR.DesktopClient.Linux.PathConstants.GetLogsPath(instanceId);
#else
    throw new PlatformNotSupportedException();
#endif
  }
}

using ControlR.Libraries.Ipc.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Libraries.Ipc;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrIpcClient<TDesktopRpc>(this IServiceCollection services)
    where TDesktopRpc : class, IDesktopClientRpcService
  {
    services.AddLogging();
    services.TryAddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
    services.TryAddTransient<IDesktopClientRpcService, TDesktopRpc>();
    return services;
  }
public static IServiceCollection AddControlrIpcClient<TDesktopRpc>(
    this IServiceCollection services,
    Func<IServiceProvider, TDesktopRpc> implementationFactory)
    where TDesktopRpc : class, IDesktopClientRpcService
  {
    services.AddLogging();
    services.TryAddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
    services.TryAddTransient<IDesktopClientRpcService>(implementationFactory);
    return services;
  }
  public static IServiceCollection AddControlrIpcServer<TAgentRpc>(this IServiceCollection services)
    where TAgentRpc : class, IAgentRpcService
  {
    services.AddLogging();
    services.TryAddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
    services.TryAddTransient<IAgentRpcService, TAgentRpc>();
    return services;
  }
  public static IServiceCollection AddControlrIpcServer<TAgentRpc>(
    this IServiceCollection services,
    Func<IServiceProvider, TAgentRpc> implementationFactory)
    where TAgentRpc : class, IAgentRpcService
  {
    services.AddLogging();
    services.TryAddSingleton<IIpcConnectionFactory, IpcConnectionFactory>();
    services.TryAddTransient<IAgentRpcService>(implementationFactory);
    return services;
  }
}
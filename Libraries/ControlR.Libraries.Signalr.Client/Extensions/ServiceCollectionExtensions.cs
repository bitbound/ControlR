using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Libraries.Signalr.Client.Extensions;
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// <para>
  ///   Creates a scoped registration in DI for <typeparamref name="TClientImpl"/>
  ///   that resolves to <typeparamref name="TClient"/>.
  /// </para>
  /// <para>
  ///   Creates a scoped registration in DI for <see cref="IHubConnection{THub}"/>.
  /// </para>
  /// <para>
  ///   Consumers should use the <see cref="IHubConnection{THub}"/> interface for
  ///   connecting to and interacting with the server.
  /// </para>
  /// </summary>
  /// <typeparam name="THub">
  ///   An interface representing the public methods on the server-side hub.
  ///   These methods will be invokable by the client.
  /// </typeparam>
  /// <typeparam name="TClient">
  ///   An interface representing the public methods on the client.
  ///   These methods will be invokable by the server.
  /// </typeparam>
  /// <typeparam name="TClientImpl">
  ///   An implementation of <typeparamref name="TClient"/>.  These methods
  ///   will handle the RPC invocations from the server.
  /// </typeparam>
  /// <param name="services"></param>
  /// <param name="clientLifetime">
  ///   The service lifetime to use for the hub connection client.
  /// </param>
  /// <returns></returns>
  public static IServiceCollection AddStronglyTypedSignalrClient<THub, TClient, TClientImpl>(
    this IServiceCollection services,
    ServiceLifetime clientLifetime)
    where THub : class
    where TClient : class
    where TClientImpl : class, TClient
  {
    if (!typeof(THub).IsInterface)
    {
      throw new InvalidOperationException("THub must be an interface.");
    }

    if (!typeof(TClient).IsInterface)
    {
      throw new InvalidOperationException("TClient must be an interface.");
    }

    services.TryAddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

    switch (clientLifetime)
    {
      case ServiceLifetime.Singleton:
        {
          services.TryAddSingleton<TClient, TClientImpl>();
          services.TryAddSingleton<IHubConnection<THub>, HubConnection<THub, TClient>>();
          break;
        }
      case ServiceLifetime.Scoped:
        {
          services.TryAddScoped<TClient, TClientImpl>();
          services.TryAddScoped<IHubConnection<THub>, HubConnection<THub, TClient>>();
          break;
        }
      case ServiceLifetime.Transient:
        {
          services.TryAddTransient<TClient, TClientImpl>();
          services.TryAddTransient<IHubConnection<THub>, HubConnection<THub, TClient>>();
          break;
        }
      default:
        throw new InvalidOperationException($"Unknown service lifetime: {clientLifetime}");
    }

    return services;
  }
}

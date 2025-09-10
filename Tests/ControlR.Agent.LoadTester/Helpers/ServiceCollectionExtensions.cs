using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Agent.LoadTester.Helpers;

internal static class ServiceCollectionExtensions
{
  public static IServiceCollection ReplaceService<TInterface, TImplementation>(
    this IServiceCollection services,
    ServiceLifetime lifetime)
    where TImplementation : class, TInterface
    where TInterface : class
  {

    services.RemoveAll<TInterface>();

    return lifetime switch
    {
      ServiceLifetime.Singleton => services.AddSingleton<TInterface, TImplementation>(),
      ServiceLifetime.Scoped => services.AddScoped<TInterface, TImplementation>(),
      ServiceLifetime.Transient => services.AddTransient<TInterface, TImplementation>(),
      _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Invalid service lifetime specified.")
    };
  }

  public static IServiceCollection ReplaceService<TInterface, TImplementation>(
    this IServiceCollection services,
    ServiceLifetime lifetime,
    TImplementation instance)
    where TImplementation : class, TInterface
    where TInterface : class
  {
    services.RemoveAll<TInterface>();

    return lifetime switch
    {
      ServiceLifetime.Singleton => services.AddSingleton<TInterface>(instance),
      ServiceLifetime.Scoped => services.AddScoped<TInterface>(sp => instance),
      ServiceLifetime.Transient => services.AddTransient<TInterface>(sp => instance),
      _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Invalid service lifetime specified.")
    };
  }

  public static IServiceCollection ReplaceService<TInterface, TImplementation>(
    this IServiceCollection services,
    ServiceLifetime lifetime,
    Func<IServiceProvider, TImplementation> factory)
    where TImplementation : class, TInterface
    where TInterface : class
  {
    services.RemoveAll<TInterface>();

    return lifetime switch
    {
      ServiceLifetime.Singleton => services.AddSingleton<TInterface>(factory),
      ServiceLifetime.Scoped => services.AddScoped<TInterface>(factory),
      ServiceLifetime.Transient => services.AddTransient<TInterface>(factory),
      _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Invalid service lifetime specified.")
    };
  }

  public static IServiceCollection RemoveImplementation<TImplementation>(
    this IServiceCollection services)
    where TImplementation : class
  {
    var implementations = services
      .Where(x => x.ImplementationType == typeof(TImplementation) ||
                  (x.ImplementationInstance is TImplementation))
      .ToList();

    foreach (var implementation in implementations)
    {
      services.Remove(implementation);
    }

    return services;
  }

  public static IServiceCollection RemoveService<TInterface>(
    this IServiceCollection services)
    where TInterface : class
  {
    services.RemoveAll<TInterface>();
    return services;
  }
}

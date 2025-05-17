using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlR.Tests.TestingUtilities;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection ReplaceImplementation<TService, TImplementation>(
    this IServiceCollection services,
    ServiceLifetime lifetime)
    where TService : class
    where TImplementation : class, TService
  {
    services.RemoveAll<TService>();

    switch (lifetime)
    {
      case ServiceLifetime.Singleton:
        services.AddSingleton<TService, TImplementation>();
        break;
      case ServiceLifetime.Scoped:
        services.AddScoped<TService, TImplementation>();
        break;
      case ServiceLifetime.Transient:
        services.AddTransient<TService, TImplementation>();
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
    }

    return services;
  }
  public static IServiceCollection ReplaceSingleton<TService, TImplementation>(
    this IServiceCollection services,
    TImplementation instance)
    where TService : class
    where TImplementation : class, TService
  {
    return services.ReplaceImplementation<TService, TImplementation>(ServiceLifetime.Singleton);
  }

  public static IServiceCollection ReplaceSingleton<TService, TImplementation>(
    this IServiceCollection services,
    ServiceLifetime lifetime,
    Func<IServiceProvider, TImplementation> factory)
    where TService : class
    where TImplementation : class, TService
  {
    var descriptor = services.Single(d => d.ServiceType == typeof(TService));
    services.Remove(descriptor);

    switch (lifetime)
    {
      case ServiceLifetime.Singleton:
        services.AddSingleton<TService>(factory);
        break;
      case ServiceLifetime.Scoped:
        services.AddScoped<TService>(factory);
        break;
      case ServiceLifetime.Transient:
        services.AddTransient<TService>(factory);
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
    }

    return services;
  }
}
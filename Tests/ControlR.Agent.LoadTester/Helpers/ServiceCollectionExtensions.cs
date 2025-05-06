using ControlR.Agent.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Agent.LoadTester.Helpers;
internal static class ServiceCollectionExtensions
{
  public static IServiceCollection ReplaceImplementation<TInterface, TImplementation>(this IServiceCollection services)
    where TImplementation : class, TInterface
    where TInterface : class
  {
    services.Remove(
     services.First(x => x.ServiceType == typeof(TInterface)));

    return services.AddSingleton<TInterface, TImplementation>();
  }

  public static IServiceCollection ReplaceImplementation<TInterface, TImplementation>(
    this IServiceCollection services,
    TImplementation instance)
    where TImplementation : class, TInterface
    where TInterface : class
  {
    services.Remove(
     services.First(x => x.ServiceType == typeof(TInterface)));

    return services.AddSingleton<TInterface>(instance);
  }

  public static IServiceCollection ReplaceImplementation<TInterface, TImplementation>(
    this IServiceCollection services,
    Func<IServiceProvider, TImplementation> factory)
    where TImplementation : class, TInterface
    where TInterface : class
  {
    services.Remove(
     services.First(x => x.ServiceType == typeof(TInterface)));

    return services.AddSingleton<TInterface>(factory);
  }

  public static IServiceCollection RemoveImplementation<TImplementation>(
    this IServiceCollection services)
    where TImplementation : class
  {
    services.Remove(
     services.First(x => x.ImplementationType == typeof(TImplementation)));
    return services;
  }
}

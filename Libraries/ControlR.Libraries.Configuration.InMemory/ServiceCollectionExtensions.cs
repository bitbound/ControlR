using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.Configuration.InMemory;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds an in-memory configuration provider that can be updated at runtime.
  /// Register IInMemoryConfigurationAccessor to update values and trigger IOptionsMonitor reloads.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configurationBuilder">The configuration builder to add the provider to.</param>
  /// <returns>The service collection for chaining.</returns>
  public static IServiceCollection AddInMemoryConfiguration(
    this IServiceCollection services,
    IConfigurationBuilder configurationBuilder)
  {
    var source = new InMemoryConfigurationSource();
    configurationBuilder.Add(source);

    services.AddSingleton(source.Provider);
    services.AddSingleton<IInMemoryConfigurationAccessor, InMemoryConfigurationAccessor>();

    return services;
  }
}

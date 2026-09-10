using ControlR.ApiClient.Interfaces.Internal;
using ControlR.ApiClient.Interfaces.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClient.Tests;

public sealed class ServiceCollectionExtensionsTests
{
  [Fact]
  public async Task AddControlrApiClientFactory_ResolvesFactoryWithDefaultOptions()
  {
    var services = new ServiceCollection();
    services.AddLogging();

    services.AddControlrApiClientFactory();

    using var provider = services.BuildServiceProvider();
    using var factory = provider.GetRequiredService<IControlrApiClientFactory>();

    var client = factory.GetOrCreateClient("a", o => o.BaseUrl = new Uri("https://server.test/"));
    Assert.NotNull(client);
    Assert.Equal(["a"], factory.GetClientNames());
    await Task.CompletedTask;
  }

  [Fact]
  public void AddControlrApiClientFactory_WhenMaxTrackedClientsIsBelowOne_FailsStartupValidation()
  {
    var services = new ServiceCollection();
    services.AddLogging();

    services.AddControlrApiClientFactory(options => options.MaxTrackedClients = 0);

    using var provider = services.BuildServiceProvider();

    // A cap below one used to mean "unlimited", the opposite of what setting a cap intends. It now
    // fails at first resolution, which is what ValidateOnStart makes loud at startup.
    Assert.Throws<OptionsValidationException>(
      () => provider.GetRequiredService<IOptions<ControlrApiClientFactoryOptions>>().Value);
  }

  [Fact]
  public void AddControlrApiClient_RegistersSubInterfacesForNarrowInjection()
  {
    var services = new ServiceCollection();
    services.AddLogging();

    services.AddControlrApiClient(options =>
    {
      options.BaseUrl = new Uri("https://server.test/");
      options.PersonalAccessToken = "pat";
    });

    using var provider = services.BuildServiceProvider();

    // Pins the TryAddTransient(Func) overload resolution: the service type must be the
    // interface, not object. A regression here silently breaks consumer DI.
    var internalApi = provider.GetRequiredService<IControlrInternalApi>();
    var v1Api = provider.GetRequiredService<IControlrV1Api>();

    // The registered service type must be the interface itself, not object. A regression in
    // overload resolution here is exactly what breaks consumer DI.
    Assert.Contains(
      services,
      d => d.ServiceType == typeof(IControlrInternalApi));
    Assert.Contains(
      services,
      d => d.ServiceType == typeof(IControlrV1Api));
    Assert.IsAssignableFrom<IControlrApi>(provider.GetRequiredService<IControlrApi>());
    Assert.NotNull(internalApi);
    Assert.NotNull(v1Api);
  }
}

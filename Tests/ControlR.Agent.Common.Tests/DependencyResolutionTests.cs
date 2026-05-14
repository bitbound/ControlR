using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Startup;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Tests;

public class DependencyResolutionTests
{
  [Theory]
  [InlineData(StartupMode.Run)]
  [InlineData(StartupMode.Uninstall)]
  internal void Build_InDevelopment_ValidatesDependencyGraph(StartupMode startupMode)
  {
    // Arrange
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = Environments.Development
    });

    builder.AddControlRAgent(
      startupMode,
      instanceId: "test",
      serverUri: new Uri("http://localhost"),
      loadAppSettings: false);

    var serviceDescriptors = builder.Services.ToList();

    // Act & Assert - In Development, Build() validates the entire dependency graph
    // and throws if any registered services have unresolved dependencies.
    using var host = builder.Build();

    foreach (var descriptor in serviceDescriptors)
    {
      if (descriptor.ServiceType.IsGenericType)
      {
        // Skip open generic types as they cannot be resolved directly.
        continue;
      }

      var service = host.Services.GetService(descriptor.ServiceType);
      Assert.NotNull(service);
    }
  }

  [Theory]
  [InlineData(StartupMode.Run)]
  [InlineData(StartupMode.Uninstall)]
  internal void Build_InProduction_Succeeds(StartupMode startupMode)
  {
    // Arrange
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = Environments.Production
    });

    builder.AddControlRAgent(
      startupMode, 
      instanceId: null, 
      serverUri: new Uri("http://localhost"),
      loadAppSettings: false);

    var serviceDescriptors = builder.Services.ToList();

    // Act & Assert
    using var host = builder.Build();
    Assert.NotNull(host);

    foreach (var descriptor in serviceDescriptors)
    {
      if (descriptor.ServiceType.IsGenericType)
      {
        // Skip open generic types as they cannot be resolved directly.
        continue;
      }

      var service = host.Services.GetService(descriptor.ServiceType);
      Assert.NotNull(service);
    }
  }
}

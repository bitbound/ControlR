using ControlR.Agent.Common.Models;
using ControlR.Agent.Common.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Tests.Startup;

public class DependencyResolutionTests
{
  [Theory]
  [InlineData(StartupMode.Run)]
  [InlineData(StartupMode.Install)]
  [InlineData(StartupMode.Uninstall)]
  internal void Build_InDevelopment_ValidatesDependencyGraph(StartupMode startupMode)
  {
    // Arrange
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = Environments.Development
    });

    builder.AddControlRAgent(startupMode, instanceId: null, serverUri: null, loadAppSettings: false);

    // Act & Assert - In Development, Build() validates the entire dependency graph
    // and throws if any registered services have unresolved dependencies.
    using var host = builder.Build();
  }

  [Theory]
  [InlineData(StartupMode.Run)]
  [InlineData(StartupMode.Install)]
  [InlineData(StartupMode.Uninstall)]
  internal void Build_InProduction_Succeeds(StartupMode startupMode)
  {
    // Arrange
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = Environments.Production
    });

    builder.AddControlRAgent(startupMode, instanceId: null, serverUri: null, loadAppSettings: false);

    // Act & Assert - In Production, Build() does not validate the dependency graph,
    // but we still verify it builds successfully.
    using var host = builder.Build();
  }
}

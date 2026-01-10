using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ControlR.DesktopClient.Tests.Startup;

public class StaticServiceProviderTests
{
  [Fact]
  internal void Build_InDevelopment_ValidatesDependencyGraph()
  {
    // Arrange
    // Configure environment as Development for validation
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Development);
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = Environments.Development
    });


    var instanceId = $"test-{Guid.NewGuid()}";
    var mockLifetime = new Mock<IControlledApplicationLifetime>();

    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services.AddControlrDesktop(instanceId);

    using var host = builder.Build();

    Assert.True(true, "Dependency graph should be valid in Development environment.");
  }

  [Fact]
  internal void Build_InProduction_Succeeds()
  {
    // Arrange
    // Configure environment as Production for validation
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Production);
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = Environments.Production
    });


    var instanceId = $"test-{Guid.NewGuid()}";
    var mockLifetime = new Mock<IControlledApplicationLifetime>();

    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services.AddControlrDesktop(instanceId);

    using var host = builder.Build();

    Assert.True(true, "Dependency graph should be valid in Production environment.");
  }
}

using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ControlR.DesktopClient.Tests.Startup;

public class StaticServiceProviderTests
{
  [LinuxOnlyTheory]
  [InlineData(DesktopEnvironmentType.X11, "Development")]
  [InlineData(DesktopEnvironmentType.Wayland, "Development")]
  [InlineData(DesktopEnvironmentType.X11, "Production")]
  [InlineData(DesktopEnvironmentType.Wayland, "Production")]
  internal void Build_ValidatesDependencyGraph_Linux(DesktopEnvironmentType desktopEnvironment, string environment)
  {
    switch (desktopEnvironment)
    {
      case DesktopEnvironmentType.X11:
        Environment.SetEnvironmentVariable("DISPLAY", ":0");
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
        break;
      case DesktopEnvironmentType.Wayland:
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
        Environment.SetEnvironmentVariable("DISPLAY", null);
        break;
    }
    Build_ValidatesDependencyGraph(environment);
  }

  [MacOnlyTheory]
  [InlineData("Development")]
  [InlineData("Production")]
  internal void Build_ValidatesDependencyGraph_Mac(string environment)
  {
    Build_ValidatesDependencyGraph(environment);
  }

  [WindowsOnlyTheory]
  [InlineData("Development")]
  [InlineData("Production")]
  internal void Build_ValidatesDependencyGraph_Windows(string environment)
  {
    Build_ValidatesDependencyGraph(environment);
  }

  private static void Build_ValidatesDependencyGraph(string environment)
  {
    // Arrange
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = environment
    });

    var instanceId = $"test-{Guid.NewGuid()}";
    var mockLifetime = new Mock<IControlledApplicationLifetime>();

    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services.AddControlrDesktop(instanceId);

    try
    {
      // Act & Assert
      using var host = builder.Build();
      Assert.NotNull(host);
    }
    finally
    {
      Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
    }
  }
}

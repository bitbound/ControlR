using Avalonia.Controls.ApplicationLifetimes;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Startup;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ControlR.DesktopClient.Tests;

public class RemoteControlHostDependencyTests
{
  [Theory]
  [InlineData(DesktopEnvironmentType.X11, "Development")]
  [InlineData(DesktopEnvironmentType.Wayland, "Development")]
  [InlineData(DesktopEnvironmentType.X11, "Production")]
  [InlineData(DesktopEnvironmentType.Wayland, "Production")]
  internal void CreateRemoteControlHostBuilder_InDevelopment_ValidatesDependencyGraph_Linux(DesktopEnvironmentType desktopEnvironment, string environment)
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

    var requestDto = new RemoteControlRequestIpcDto(
      SessionId: Guid.NewGuid(),
      WebsocketUri: new Uri("wss://localhost:5001"),
      TargetSystemSession: 1,
      TargetProcessId: 1234,
      ViewerConnectionId: "test-connection-id",
      DeviceId: Guid.NewGuid(),
      NotifyUserOnSessionStart: false,
      RequireConsent: false,
      ViewerName: "Test Viewer");

    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);

    try
    {
      var desktopClientApp = BuildDesktopClientAppHost(environment);
      var factory = desktopClientApp.Services.GetRequiredService<IRemoteControlHostBuilderFactory>();
      var builder = factory.CreateHostBuilder(requestDto);
      var serviceDescriptors = builder.Services.ToList();
      using var remoteControlHost = builder.Build();

      Assert.NotNull(remoteControlHost);

      foreach (var descriptor in serviceDescriptors)
      {
        if (descriptor.ServiceType.IsGenericType)
        {
          // Skip open generic types as they cannot be resolved directly.
          continue;
        }

       var service = remoteControlHost.Services.GetService(descriptor.ServiceType);
        Assert.NotNull(service);
      }
    }
    finally
    {
      Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
    }
  }

  private static IHost BuildDesktopClientAppHost(string environment)
  {
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
      EnvironmentName = environment
    });

    var instanceId = $"test-{Guid.NewGuid()}";
    var mockLifetime = new Mock<IControlledApplicationLifetime>();

    builder.Services.AddSingleton(mockLifetime.Object);
    builder.Services
      .AddDesktopShellServices(instanceId)
      .AddDesktopAppPlatformServices();

    return builder.Build();
  }
}

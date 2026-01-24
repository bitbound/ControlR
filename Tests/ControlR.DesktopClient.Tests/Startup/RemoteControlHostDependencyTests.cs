using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Tests.TestingUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ControlR.DesktopClient.Tests.Startup;

public class RemoteControlHostDependencyTests
{
  [LinuxOnlyTheory]
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
    CreateRemoteControlHostBuilder_ValidatesDependencyGraph(environment);
  }

  [MacOnlyTheory]
  [InlineData("Development")]
  [InlineData("Production")]  
  internal void CreateRemoteControlHostBuilder_InDevelopment_ValidatesDependencyGraph_Mac(string environment)
  {
    CreateRemoteControlHostBuilder_ValidatesDependencyGraph(environment);
  }

  [WindowsOnlyTheory]
  [InlineData("Development")]
  [InlineData("Production")]
  internal void CreateRemoteControlHostBuilder_InDevelopment_ValidatesDependencyGraph_Windows(string environment)
  {
    CreateRemoteControlHostBuilder_ValidatesDependencyGraph(environment);
  }

  private void CreateRemoteControlHostBuilder_ValidatesDependencyGraph(string environment)
  {
    // Arrange
    var ipcClientAccessor = new Mock<IIpcClientAccessor>();
    var userInteractionService = new Mock<IUserInteractionService>();
    var desktopClientOptions = new Mock<IOptionsMonitor<DesktopClientOptions>>();
    var appLifetimeNotifier = new Mock<IAppLifetimeNotifier>();
    var mockLogger = new Mock<ILogger<RemoteControlHostManager>>();
    var timeProvider = new FakeTimeProvider(DateTimeOffset.Now);

    desktopClientOptions
      .Setup(x => x.CurrentValue)
      .Returns(new DesktopClientOptions { InstanceId = $"test-{Guid.NewGuid()}" });

    var hostManager = new RemoteControlHostManager(
      timeProvider,
      userInteractionService.Object,
      ipcClientAccessor.Object,
      appLifetimeNotifier.Object,
      desktopClientOptions.Object,
      mockLogger.Object);

    var requestDto = new RemoteControlRequestIpcDto(
      SessionId: Guid.NewGuid(),
      WebsocketUri: new Uri("wss://localhost:5001"),
      TargetSystemSession: 1,
      TargetProcessId: 1234,
      ViewerConnectionId: "test-connection-id",
      DeviceId: Guid.NewGuid(),
      NotifyUserOnSessionStart: false,
      RequireConsent: false,
      DataFolder: Path.GetTempPath(),
      ViewerName: "Test Viewer");

    // Configure environment as Development for validation
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);

    try
    {
      // Act - Get the builder using the internal method
      var builder = hostManager.CreateRemoteControlHostBuilder(requestDto);

      // Assert - In Development, Build() validates the entire dependency graph
      // and throws if any registered services have unresolved dependencies.
      using var host = builder.Build();

      Assert.NotNull(host);
    }
    finally
    {
      Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
    }
  }
}

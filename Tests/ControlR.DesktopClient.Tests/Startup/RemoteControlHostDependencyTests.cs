using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace ControlR.DesktopClient.Tests.Startup;

public class RemoteControlHostDependencyTests
{
  [Fact]
  internal void CreateRemoteControlHostBuilder_InDevelopment_ValidatesDependencyGraph()
  {
    // Arrange
    var mockIpcAccessor = new Mock<IIpcClientAccessor>();
    var mockUserInteractionService = new Mock<IUserInteractionService>();
    var mockDesktopClientOptions = new Mock<IOptionsMonitor<DesktopClientOptions>>();
    var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<RemoteControlHostManager>>();

    mockDesktopClientOptions
      .Setup(x => x.CurrentValue)
      .Returns(new DesktopClientOptions { InstanceId = Guid.NewGuid().ToString() });

    var hostManager = new RemoteControlHostManager(
      mockUserInteractionService.Object,
      mockDesktopClientOptions.Object,
      mockIpcAccessor.Object,
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
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Development);

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

  [Fact]
  internal void CreateRemoteControlHostBuilder_InProduction_Succeeds()
  {
    // Arrange
    var mockIpcAccessor = new Mock<IIpcClientAccessor>();
    var mockUserInteractionService = new Mock<IUserInteractionService>();
    var mockDesktopClientOptions = new Mock<IOptionsMonitor<DesktopClientOptions>>();
    var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<RemoteControlHostManager>>();

    mockDesktopClientOptions
      .Setup(x => x.CurrentValue)
      .Returns(new DesktopClientOptions { InstanceId = Guid.NewGuid().ToString() });

    var hostManager = new RemoteControlHostManager(
      mockUserInteractionService.Object,
      mockDesktopClientOptions.Object,
      mockIpcAccessor.Object,
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

    // Configure environment as Production
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Production);

    try
    {
      // Act - Get the builder using the internal method
      var builder = hostManager.CreateRemoteControlHostBuilder(requestDto);

      // Assert - In Production, Build() does not validate the dependency graph,
      // but we still verify it builds successfully.
      using var host = builder.Build();

      Assert.NotNull(host);
    }
    finally
    {
      Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
    }
  }
}

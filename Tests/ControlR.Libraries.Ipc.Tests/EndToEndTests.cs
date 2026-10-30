using ControlR.Libraries.Ipc.Interfaces;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.TestingUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ControlR.Libraries.Ipc.Tests;

public class EndToEndTests(ITestOutputHelper testOutputHelper) : IAsyncLifetime
{
  private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

  private CancellationTokenSource _cts = null!;

  [Fact]
  public async Task ClientCanCallServerMethod()
  {
    // Arrange
    var receivedResponse = false;
    var tcs = new TaskCompletionSource<bool>();

    var mockAgentService = new Mock<IAgentRpcService>();
    mockAgentService
      .Setup(m => m.SendChatResponse(It.IsAny<ChatResponseIpcDto>()))
      .ReturnsAsync(true)
      .Callback<ChatResponseIpcDto>(dto =>
      {
        receivedResponse = true;
        tcs.SetResult(true);
      });

    var mockClientService = new Mock<IDesktopClientRpcService>();

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddLogging(logBuilder =>
    {
      logBuilder.AddProvider(new XunitLoggerProvider(_testOutputHelper));
    });

    serviceCollection.AddControlrIpcServer(_ => mockAgentService.Object);
    serviceCollection.AddControlrIpcClient(_ => mockClientService.Object);
    var services = serviceCollection.BuildServiceProvider();

    var connectionFactory = services.GetRequiredService<IIpcConnectionFactory>();
    var pipeName = Guid.NewGuid().ToString();
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    var server = await connectionFactory.CreateServer(pipeName);
    var client = await connectionFactory.CreateClient(".", pipeName);

    // Act
    var serverTask = server.WaitForConnection(cts.Token);
    await client.Connect(cts.Token);
    await serverTask;

    server.Start();
    client.Start();

    var result = await client.Server.SendChatResponse(
      new ChatResponseIpcDto(
        Guid.NewGuid(),
        123,
        "response message",
        "user1",
        "conn1",
        DateTimeOffset.Now));

    // Assert
    Assert.True(result);
    Assert.True(await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    Assert.True(receivedResponse);

    mockAgentService.Verify(m => m.SendChatResponse(It.IsAny<ChatResponseIpcDto>()), Times.Once);

    // Cleanup
    cts?.Cancel();
    cts?.Dispose();
    server?.Dispose();
    client?.Dispose();
    services?.Dispose();
  }

  public async ValueTask DisposeAsync()
  {
    _cts?.Cancel();
    _cts?.Dispose();
    await Task.CompletedTask;
  }

  public async ValueTask InitializeAsync()
  {
    _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await Task.CompletedTask;
  }

  [Fact]
  public async Task ServerCanCallClientMethod()
  {
    // Arrange
    var receivedMessage = "";
    var tcs = new TaskCompletionSource<bool>();

    var mockClientService = new Mock<IDesktopClientRpcService>();
    mockClientService
      .Setup(m => m.ReceiveChatMessage(It.IsAny<ChatMessageIpcDto>()))
      .Returns<ChatMessageIpcDto>(dto =>
      {
        receivedMessage = dto.Message;
        tcs.SetResult(true);
        return Task.CompletedTask;
      });

    var mockAgentService = new Mock<IAgentRpcService>();

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddLogging(logBuilder =>
    {
      logBuilder.AddProvider(new XunitLoggerProvider(_testOutputHelper));
    });
    
    serviceCollection.AddControlrIpcServer(_ => mockAgentService.Object);
    serviceCollection.AddControlrIpcClient(_ => mockClientService.Object);
    var services = serviceCollection.BuildServiceProvider();

    var connectionFactory = services.GetRequiredService<IIpcConnectionFactory>();
    var pipeName = Guid.NewGuid().ToString();
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    var server = await connectionFactory.CreateServer(pipeName);
    var client = await connectionFactory.CreateClient(".", pipeName);

    // Act
    var serverTask = server.WaitForConnection(cts.Token);
    await client.Connect(cts.Token);
    await serverTask;

    server.Start();
    client.Start();

    var testMessage = "Hello from server!";
    await server.Client.ReceiveChatMessage(
      new ChatMessageIpcDto(
        Guid.NewGuid(),
        testMessage,
        "user1",
        "email@test.com",
        1,
        123,
        "conn1",
        DateTimeOffset.Now));

    // Assert
    Assert.True(await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    Assert.Equal(testMessage, receivedMessage);

    mockClientService.Verify(m => m.ReceiveChatMessage(It.IsAny<ChatMessageIpcDto>()), Times.Once);

    // Cleanup
    cts?.Cancel();
    cts?.Dispose();
    server?.Dispose();
    client?.Dispose();
    services?.Dispose();
  }

  [Fact]
  public async Task ServerCanSendRemoteControlRequestToClient()
  {
    var tcs = new TaskCompletionSource<RemoteControlRequestIpcDto>();
    var sessionId = Guid.NewGuid();
    var commandPolicy = RemoteControlCommandPolicyDto.Create(
      sessionId,
      [RemoteControlViewerDtoType.TypeText]);

    var mockClientService = new Mock<IDesktopClientRpcService>();
    mockClientService
      .Setup(m => m.ReceiveRemoteControlRequest(It.IsAny<RemoteControlRequestIpcDto>()))
      .Returns<RemoteControlRequestIpcDto>((dto) =>
      {
        tcs.SetResult(dto);
        return Task.FromResult(Result.Ok());
      });

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddLogging(logBuilder =>
    {
      logBuilder.AddProvider(new XunitLoggerProvider(_testOutputHelper));
    });

    serviceCollection.AddControlrIpcServer(_ => Mock.Of<IAgentRpcService>());
    serviceCollection.AddControlrIpcClient(_ => mockClientService.Object);
    var services = serviceCollection.BuildServiceProvider();

    var connectionFactory = services.GetRequiredService<IIpcConnectionFactory>();
    var pipeName = Guid.NewGuid().ToString();
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    var server = await connectionFactory.CreateServer(pipeName);
    var client = await connectionFactory.CreateClient(".", pipeName);

    var serverTask = server.WaitForConnection(cts.Token);
    await client.Connect(cts.Token);
    await serverTask;

    server.Start();
    client.Start();

    var request = new RemoteControlRequestIpcDto(
      sessionId,
      new Uri("wss://relay.example.com/remote-control"),
      TargetSystemSession: 1,
      TargetProcessId: 4242,
      DeviceId: Guid.NewGuid(),
      NotifyUserOnSessionStart: false,
      RequireConsent: false,
      ViewerConnectionId: "conn-1",
      ViewerName: "jared")
    {
      CommandPolicy = commandPolicy,
    };
    await server.Client.ReceiveRemoteControlRequest(request);

    var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    Assert.Equal(sessionId, received.SessionId);
    Assert.Equal("jared", received.ViewerName);
    Assert.NotNull(received.CommandPolicy);
    Assert.True(received.CommandPolicy!.IsBoundTo(sessionId));
    Assert.Equal(commandPolicy.AllowedDtoTypes, received.CommandPolicy!.AllowedDtoTypes);

    cts?.Cancel();
    cts?.Dispose();
    server?.Dispose();
    client?.Dispose();
    services?.Dispose();
  }
}
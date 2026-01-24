using Avalonia.Controls.ApplicationLifetimes;
using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Services;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.Ipc.Interfaces;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Services;
using ControlR.Tests.TestingUtilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Diagnostics;

namespace ControlR.DesktopClient.Tests;

public class IpcClientManagerTests : IAsyncLifetime
{
  private readonly CancellationTokenSource _cts = Debugger.IsAttached 
    ? new CancellationTokenSource()
    : new CancellationTokenSource(TimeSpan.FromSeconds(10));

  private IpcClientManager _clientManager = null!;
  private FakeTimeProvider _fakeTimeProvider = null!;
  private IIpcConnectionFactory _ipcConnectionFactory = null!;
  private IIpcServer _ipcServer = null!;
  private string _pipeName = string.Empty;
  private Mock<IAgentRpcService> _rpcAgent = null!;
  private Mock<IDesktopClientRpcService> _rpcClient = null!;

  public Task DisposeAsync()
  {
    _cts.Dispose();
    return Task.CompletedTask;
  }
  public async Task InitializeAsync()
  {
    var instanceId = Guid.NewGuid().ToString();
    _pipeName = IpcPipeNames.GetPipeName(instanceId);
    _fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    _rpcAgent = CreateMockAgentRpcService();
    _rpcClient = CreateMockDesktopClientRpcService();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<TimeProvider>(_fakeTimeProvider);
    services.AddControlrIpcClient(_ => _rpcClient.Object);
    services.AddControlrIpcServer(_ => _rpcAgent.Object);

    var serviceProvider = services.BuildServiceProvider();
    _ipcConnectionFactory = serviceProvider.GetRequiredService<IIpcConnectionFactory>();
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

    var mockAppLifetime = new Mock<IControlledApplicationLifetime>();
    var desktopClientOptions = Options.Create(new DesktopClientOptions
    {
      InstanceId = instanceId
    });

    _clientManager = new IpcClientManager(
      _fakeTimeProvider,
      _ipcConnectionFactory,
      mockAppLifetime.Object,
      new WeakReferenceMessenger(),
      desktopClientOptions,
      loggerFactory.CreateLogger<IpcClientManager>());


    _ipcServer = await _ipcConnectionFactory.CreateServer(_pipeName);
  }
  [Fact]
  public async Task IpcClientManager_ReconnectsSuccessfully()
  {
    // Start the IPC server and client manager
    var serverWaitTask = _ipcServer.WaitForConnection(_cts.Token);
    await _clientManager.StartAsync(_cts.Token);
    await serverWaitTask;
    _ipcServer.Start();

    // Wait for the client to connect
    using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    await Waiter.Default.WaitFor(
      () => _clientManager.TryGetClient(out var client) && client.IsConnected,
      cancellationToken: waitCts.Token);

    // Send a chat response to verify the connection
    Assert.True(_clientManager.TryGetClient(out var client));
    await client.WaitForConnected(_cts.Token);
    var chatResult = await client.Server.SendChatResponse(CreateChatResponseDto());
    Assert.True(chatResult);

    // Now simulate a disconnection
    _ipcServer.Dispose();

    // Wait Task.Delay inside IpcClientManager
    var waitResult = await _fakeTimeProvider.WaitForWaiters(x => x > 0);
    Assert.True(waitResult, "Expected there to be waiters on the FakeTimeProvider.");

    // Advance time to trigger the reconnect attempt
    _fakeTimeProvider.Advance(TimeSpan.FromSeconds(6));

    // Recreate and start the IPC server to accept the reconnect
    _ipcServer = await _ipcConnectionFactory.CreateServer(_pipeName);
    await _ipcServer.WaitForConnection(_cts.Token);
    _ipcServer.Start();

    // Wait for the client to connect
    using var waitCts2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    await Waiter.Default.WaitFor(
      () => _clientManager.TryGetClient(out var _),
      cancellationToken: waitCts2.Token);

    // Send a chat response to verify the re-connection
    Assert.True(_clientManager.TryGetClient(out client));
    await client.WaitForConnected(_cts.Token);
    chatResult = await client.Server.SendChatResponse(CreateChatResponseDto());
    Assert.True(chatResult);
  }

  private static Mock<IAgentRpcService> CreateMockAgentRpcService()
  {
    var mock = new Mock<IAgentRpcService>();
    mock
      .Setup(x => x.SendChatResponse(It.IsAny<ChatResponseIpcDto>()))
      .ReturnsAsync(true);
    return mock;
  }
  private static Mock<IDesktopClientRpcService> CreateMockDesktopClientRpcService()
  {
    var mock = new Mock<IDesktopClientRpcService>();
    mock
      .Setup(x => x.CheckOsPermissions(It.IsAny<CheckOsPermissionsIpcDto>()))
      .ReturnsAsync(new CheckOsPermissionsResponseIpcDto(true));

    mock
      .Setup(x => x.GetDesktopPreview(It.IsAny<DesktopPreviewRequestIpcDto>())) 
      .ReturnsAsync(new DesktopPreviewResponseIpcDto(GetJpegData(), true));
    return mock;
  }
  private static byte[] GetJpegData()
  {
    return Enumerable
      .Range(0, 5_000)
      .Select(i => (byte)(i % 256))
      .ToArray();
  }

  private ChatResponseIpcDto CreateChatResponseDto()
  {
    var sessionId = Guid.NewGuid();
    return new ChatResponseIpcDto(
      SessionId: sessionId,
      DesktopUiProcessId: 100,
      Message: "Some message.",
      SenderUsername: "TestUser",
      ViewerConnectionId: "asdf-1234",
      Timestamp: _fakeTimeProvider.GetUtcNow());
  }
}
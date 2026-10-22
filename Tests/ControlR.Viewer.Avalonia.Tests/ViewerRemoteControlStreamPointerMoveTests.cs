using Bitbound.SimpleMessenger;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Hubs;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client;
using ControlR.Libraries.Viewer.Common;
using ControlR.Libraries.Viewer.Common.Models;
using ControlR.Libraries.WebSocketRelay.Client;
using ControlR.Viewer.Avalonia.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Viewer.Avalonia.Tests;

public class ViewerRemoteControlStreamPointerMoveTests
{
  [Fact]
  public async Task SendPointerMove_WhileSendInFlight_SendsOnlyNewestPosition()
  {
    var firstSendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var sent = new List<MovePointerDto>();
    var sendCount = 0;

    var hub = new Mock<IHubConnection<IViewerHub>>();
    hub
      .Setup(x => x.Send(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
      .Returns((string _, object?[] args, CancellationToken _) =>
      {
        var request = (SendRemoteControlDtoToAgentRequestDto)args[0]!;
        lock (sent)
        {
          sent.Add(request.Wrapper.GetPayload<MovePointerDto>());
        }

        return Interlocked.Increment(ref sendCount) == 1
          ? firstSendGate.Task
          : Task.CompletedTask;
      });

    await using var stream = CreateStream(hub.Object);
    await InitializeSession(stream);

    var first = stream.SendPointerMove(0.1, 0.1, CancellationToken.None);

    // The first send is still in flight, so each of these only replaces the pending position.
    for (var i = 2; i <= 9; i++)
    {
      await stream.SendPointerMove(i / 10.0, i / 10.0, CancellationToken.None);
    }

    firstSendGate.SetResult();
    await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    Assert.Equal(2, Volatile.Read(ref sendCount));
    Assert.Equal(0.9, sent[1].NormalizedX);
    Assert.Equal(0.9, sent[1].NormalizedY);
  }

  private static ViewerRemoteControlStream CreateStream(IHubConnection<IViewerHub> hub)
  {
    return new ViewerRemoteControlStream(
      TimeProvider.System,
      Mock.Of<IMessenger>(),
      Mock.Of<IMemoryProvider>(),
      Mock.Of<IWaiter>(),
      Mock.Of<IStreamMetrics>(),
      hub,
      NullLogger<ViewerRemoteControlStream>.Instance);
  }

  private static async Task InitializeSession(ViewerRemoteControlStream stream)
  {
    var session = new RemoteControlSession(
      FakeDeviceState.CreateDefaultDevice(),
      targetSystemSession: 1,
      targetProcessId: 42,
      DesktopSessionType.Console);

    // The session is stored before the socket is opened, so a refused connection still leaves the
    // stream able to build a request and reach the hub.
    try
    {
      await stream.Connect(
        session,
        new Uri("ws://127.0.0.1:1/"),
        new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
    }
    catch (Exception)
    {
      // Expected. There is no relay listening.
    }
  }
}

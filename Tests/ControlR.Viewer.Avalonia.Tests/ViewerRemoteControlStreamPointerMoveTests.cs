using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Viewer.Common;
using ControlR.Libraries.Viewer.Common.Models;
using ControlR.Libraries.WebSocketRelay.Client;
using ControlR.Viewer.Avalonia.Tests.Fakes;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlR.Viewer.Avalonia.Tests;

public class ViewerRemoteControlStreamPointerMoveTests
{
  [Fact]
  public async Task SendPointerMove_WhileSendInFlight_SendsOnlyNewestPosition()
  {
    var firstSendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var recordCount = 0;

    var metrics = new Mock<IStreamMetrics>();
    metrics
      .Setup(x => x.RecordBytesOut(It.IsAny<TransferRecord>()))
      .Callback(() =>
      {
        if (Interlocked.Increment(ref recordCount) == 1)
        {
          firstSendGate.Task.Wait(TimeSpan.FromSeconds(10));
        }
      });

    using var listener = new HttpListener();
    var listenerErrors = new List<string>();
    var (port, received) = StartListener(listener, listenerErrors);

    await using var stream = CreateStream(metrics.Object, listenerErrors);
    await InitializeSession(stream, port);

    // The first send must park inside RecordBytesOut on a background thread so the remaining
    // moves can arrive while it is still mid-send.  Each of those only replaces the pending
    // position.  Wait for the park to confirm the send is actually in flight.
    var first = Task.Run(
      () => stream.SendPointerMove(0.1, 0.1, TestContext.Current.CancellationToken),
      TestContext.Current.CancellationToken);
    await WaitUntilAsync(() => Volatile.Read(ref recordCount) == 1, TimeSpan.FromSeconds(5));

    for (var i = 2; i <= 9; i++)
    {
      await stream.SendPointerMove(i / 10.0, i / 10.0, TestContext.Current.CancellationToken);
    }

    firstSendGate.SetResult();
    await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    try
    {
      await WaitUntilAsync(
        () => received.Count >= 2,
        TimeSpan.FromSeconds(5));
    }
    catch (TimeoutException ex)
    {
      throw new TimeoutException(
        $"{ex.Message} received={received.Count}; recordCount={Volatile.Read(ref recordCount)}; errors=[{string.Join(" | ", listenerErrors)}]",
        ex);
    }

    Assert.Equal(2, Volatile.Read(ref recordCount));
    Assert.Equal(2, received.Count);
    Assert.Equal(0.1, received[0].NormalizedX);
    Assert.Equal(0.9, received[1].NormalizedX);
    Assert.Equal(0.9, received[1].NormalizedY);
  }

  private static ViewerRemoteControlStream CreateStream(IStreamMetrics streamMetrics, List<string> errors)
  {
    return new ViewerRemoteControlStream(
      TimeProvider.System,
      Mock.Of<IMessenger>(),
      new MemoryProvider(),
      Waiter.Default,
      streamMetrics,
      new RecordingLogger(errors));
  }

  private static async Task InitializeSession(ViewerRemoteControlStream stream, int port)
  {
    var session = new RemoteControlSession(
      FakeDeviceState.CreateDefaultDevice(),
      targetSystemSession: 1,
      targetProcessId: 42,
      DesktopSessionType.Console);

    // The connect token stays linked to the connection's read loop for its whole lifetime,
    // so it must not expire while the session is still in use.
    await stream.Connect(
      session,
      new Uri($"ws://127.0.0.1:{port}/"),
      TestContext.Current.CancellationToken);
  }

  private static (int Port, List<MovePointerDto> Received) StartListener(HttpListener listener, List<string> errors)
  {
    var received = new List<MovePointerDto>();
    var random = new Random();

    for (var attempt = 0; attempt < 20; attempt++)
    {
      var port = random.Next(45_000, 60_000);
      listener.Prefixes.Clear();
      listener.Prefixes.Add($"http://127.0.0.1:{port}/");

      try
      {
        listener.Start();
      }
      catch (HttpListenerException)
      {
        continue;
      }

      _ = Task.Run(async () =>
      {
        while (listener.IsListening)
        {
          HttpListenerContext context;
          try
          {
            context = await listener.GetContextAsync();
          }
          catch (Exception)
          {
            return;
          }

          if (!context.Request.IsWebSocketRequest)
          {
            context.Response.StatusCode = 400;
            context.Response.Close();
            continue;
          }

          _ = Task.Run(async () =>
          {
            try
            {
              var wsContext = await context.AcceptWebSocketAsync(null);
              var socket = wsContext.WebSocket;
              var buffer = new byte[64 * 1024];

              while (socket.State == WebSocketState.Open)
              {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                  result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                  if (result.MessageType == WebSocketMessageType.Close)
                  {
                    break;
                  }

                  message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                  break;
                }

                var wrapper = MessagePackSerializer.Deserialize<RemoteControlDesktopWrapper>(
                  message.ToArray());
                lock (received)
                {
                  received.Add(wrapper.GetPayload<MovePointerDto>());
                }
              }
            }
            catch (Exception ex)
            {
              lock (errors)
              {
                errors.Add(ex.ToString());
              }
            }
          });
        }
      });

      return (port, received);
    }

    throw new InvalidOperationException("Could not bind a free loopback port.");
  }

  private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
  {
    var sw = Stopwatch.StartNew();
    while (!condition())
    {
      if (sw.Elapsed >= timeout)
      {
        throw new TimeoutException($"Condition not met within {timeout}.");
      }

      await Task.Delay(20);
    }
  }

  private sealed class RecordingLogger(List<string> errors) : ILogger<ViewerRemoteControlStream>
  {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      var message = formatter(state, exception);
      lock (errors)
      {
        errors.Add(exception is null ? message : $"{message} :: {exception}");
      }
    }
  }
}

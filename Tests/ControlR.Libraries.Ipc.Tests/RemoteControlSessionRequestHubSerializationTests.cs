using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using MessagePack;
using Xunit;

namespace ControlR.Libraries.Ipc.Tests;

/// <summary>
/// A remote control session request travels over SignalR's MessagePack protocol, which writes every
/// member keyed by its name. A viewer that predates
/// <see cref="RemoteControlSessionRequestDto.ViewerSupportsRemoteControlDtoHub"/> sends a payload with no
/// such key, so the server must accept the missing key and read it as the relay transport. That default is
/// what keeps a pre-v0.30.x viewer receiving its DTOs over the WebSocket relay instead of a hub method it
/// never implements.
/// </summary>
public class RemoteControlSessionRequestHubSerializationTests
{
  [Fact]
  public void RemoteControlSessionRequestDto_CurrentViewerFlag_RoundTrips()
  {
    var original = new RemoteControlSessionRequestDto(
      Guid.NewGuid(),
      new Uri("wss://relay.example.com/remote-control"),
      1,
      4242,
      Guid.NewGuid(),
      NotifyUserOnSessionStart: false,
      RequireConsent: false)
    {
      ViewerConnectionId = "conn1",
      ViewerName = "jared",
      ViewerSupportsRemoteControlDtoHub = true
    };

    var bytes = MessagePackSerializer.Serialize(original, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<RemoteControlSessionRequestDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(restored.ViewerSupportsRemoteControlDtoHub);
    Assert.Equal("conn1", restored.ViewerConnectionId);
    Assert.Equal("jared", restored.ViewerName);
  }

  [Fact]
  public void RemoteControlSessionRequestDto_FromLegacyViewerWithoutTransportFlag_ReadsBackAsRelay()
  {
    var legacy = new LegacyRemoteControlSessionRequestPayload(
      SessionId: Guid.NewGuid(),
      WebsocketUri: new Uri("wss://relay.example.com/remote-control"),
      TargetSystemSession: 1,
      TargetProcessId: 4242,
      DeviceId: Guid.NewGuid(),
      NotifyUserOnSessionStart: false,
      RequireConsent: true);

    var bytes = MessagePackSerializer.Serialize(legacy, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<RemoteControlSessionRequestDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(restored.ViewerSupportsRemoteControlDtoHub);
    Assert.Equal(legacy.SessionId, restored.SessionId);
    Assert.Equal(legacy.DeviceId, restored.DeviceId);
    Assert.Equal(4242, restored.TargetProcessId);
    Assert.True(restored.RequireConsent);
  }

  /// <summary>
  /// The session request shape as it existed before the transport flag was added: same keys, no flag.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyRemoteControlSessionRequestPayload(
    Guid SessionId,
    Uri WebsocketUri,
    int TargetSystemSession,
    int TargetProcessId,
    Guid DeviceId,
    bool NotifyUserOnSessionStart,
    bool RequireConsent);
}

using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using MessagePack;
using Xunit;

namespace ControlR.Libraries.Ipc.Tests;

/// <summary>
/// The command policy rides the IPC channel inside the session request. A desktop client must
/// recover the seal exactly, since a policy that fails seal verification drops every command.
/// An agent that predates command policies writes no CommandPolicy key, and the new desktop
/// client must read that as no policy rather than fail.
/// </summary>
public class RemoteControlCommandPolicySerializationTests
{
  [Fact]
  public void CommandPolicy_RoundTripsWithIntactSeal()
  {
    var policy = RemoteControlCommandPolicyDto.Create(
      Guid.NewGuid(),
      [
        RemoteControlViewerDtoType.TypeText,
        RemoteControlViewerDtoType.KeyEvent,
        RemoteControlViewerDtoType.MovePointer,
      ]);

    var bytes = MessagePackSerializer.Serialize(policy, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<RemoteControlCommandPolicyDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(policy.SessionId, restored.SessionId);
    Assert.Equal(policy.PolicyVersion, restored.PolicyVersion);
    Assert.Equal(policy.PolicyHash, restored.PolicyHash);
    Assert.Equal(policy.AllowedDtoTypes, restored.AllowedDtoTypes);
    Assert.True(restored.IsBoundTo(policy.SessionId));
  }

  [Fact]
  public void SessionRequest_FromLegacyAgentWithoutCommandPolicy_ReadsBackAsNull()
  {
    var legacy = new LegacyRemoteControlRequestPayload(
      SessionId: Guid.NewGuid(),
      WebsocketUri: new Uri("wss://relay.example.com/remote-control"),
      TargetSystemSession: 1,
      TargetProcessId: 4242,
      DeviceId: Guid.NewGuid(),
      NotifyUserOnSessionStart: false,
      RequireConsent: false,
      ViewerConnectionId: "conn1",
      ViewerName: "jared",
      ViewerSupportsRemoteControlDtoHub: true);

    var bytes = MessagePackSerializer.Serialize(legacy, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<RemoteControlRequestIpcDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Null(restored.CommandPolicy);
    Assert.Equal(legacy.SessionId, restored.SessionId);
  }

  /// <summary>
  /// The session request shape as it existed before command policies: same keys minus the policy,
  /// including the hub transport flag that no longer exists on the current record.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyRemoteControlRequestPayload(
    Guid SessionId,
    Uri WebsocketUri,
    int TargetSystemSession,
    int TargetProcessId,
    Guid DeviceId,
    bool NotifyUserOnSessionStart,
    bool RequireConsent,
    string? ViewerConnectionId,
    string? ViewerName,
    bool ViewerSupportsRemoteControlDtoHub);
}

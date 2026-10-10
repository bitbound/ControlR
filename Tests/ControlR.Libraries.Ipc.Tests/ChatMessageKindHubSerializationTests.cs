using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Enums;
using MessagePack;
using Xunit;

namespace ControlR.Libraries.Ipc.Tests;

/// <summary>
/// Chat hub DTOs travel over SignalR's MessagePack protocol, which writes every member keyed by its name. A viewer
/// or agent that predates <see cref="ChatMessageKind"/> sends a payload with no Kind key, and a window close
/// travels on the response path, so both hub shapes must accept the missing key and read it as
/// <see cref="ChatMessageKind.TextSend"/>.
/// </summary>
public class ChatMessageKindHubSerializationTests
{
  [Fact]
  public void ChatMessageHubDto_FromLegacyViewerWithoutKind_ReadsBackAsTextSend()
  {
    var legacy = new LegacyChatMessageHubPayload(
      DeviceId: Guid.NewGuid(),
      SessionId: Guid.NewGuid(),
      Message: "an old viewer message",
      SenderName: "jared",
      SenderEmail: "jared@example.com",
      TargetSystemSession: 1,
      TargetProcessId: 42,
      Timestamp: DateTimeOffset.Now,
      ViewerConnectionId: "conn1");

    var bytes = MessagePackSerializer.Serialize(legacy, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<ChatMessageHubDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(ChatMessageKind.TextSend, restored.Kind);
    Assert.Equal("an old viewer message", restored.Message);
  }

  /// <summary>
  /// The reverse direction: the sender writes a Kind key holding a window close, and the receiving peer's shape
  /// predates the enum, so that key is foreign to it. The foreign key is dropped and the peer's own keys still
  /// decode, which is what keeps an old viewer reading the body of such a payload.
  /// </summary>
  [Fact]
  public void ChatMessageHubDto_KindWindowClose_LegacyViewerReadsItsOwnKeysAndDropsKind()
  {
    var original = new ChatMessageHubDto(
      Guid.NewGuid(),
      Guid.NewGuid(),
      "User closed the chat window.",
      "jared",
      "jared@example.com",
      1,
      42,
      DateTimeOffset.Now,
      "conn1",
      ChatMessageKind.WindowClose);

    var bytes = MessagePackSerializer.Serialize(original, cancellationToken: TestContext.Current.CancellationToken);

    var legacyRead = MessagePackSerializer.Deserialize<LegacyChatMessageHubPayload>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("User closed the chat window.", legacyRead.Message);
    Assert.Equal(original.SessionId, legacyRead.SessionId);
  }

  [Fact]
  public void ChatMessageHubDto_TextSend_RoundTripsWithItsKind()
  {
    var original = new ChatMessageHubDto(
      Guid.NewGuid(),
      Guid.NewGuid(),
      "plain text",
      "jared",
      "jared@example.com",
      1,
      42,
      DateTimeOffset.Now,
      "conn1",
      ChatMessageKind.TextSend);

    var bytes = MessagePackSerializer.Serialize(original, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<ChatMessageHubDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(ChatMessageKind.TextSend, restored.Kind);
  }

  [Fact]
  public void ChatResponseHubDto_FromLegacyAgentWithoutKind_ReadsBackAsTextSend()
  {
    var legacy = new LegacyChatResponseHubPayload(
      SessionId: Guid.NewGuid(),
      DesktopSessionProcessId: 42,
      Message: "an old desktop reply",
      SenderUsername: "DESKTOP-ABC",
      ViewerConnectionId: "conn1",
      Timestamp: DateTimeOffset.Now);

    var bytes = MessagePackSerializer.Serialize(legacy, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<ChatResponseHubDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(ChatMessageKind.TextSend, restored.Kind);
    Assert.Equal("an old desktop reply", restored.Message);
  }

  /// <summary>
  /// The reverse direction on the path a window close actually travels: the close reaches a viewer whose shape has no
  /// Kind member, so the payload must still decode and its text must still arrive.
  /// </summary>
  [Fact]
  public void ChatResponseHubDto_KindWindowClose_LegacyViewerReadsItsOwnKeysAndDropsKind()
  {
    var original = new ChatResponseHubDto(
      Guid.NewGuid(),
      42,
      "User closed the chat window.",
      "DESKTOP-ABC",
      "conn1",
      DateTimeOffset.Now,
      ChatMessageKind.WindowClose);

    var bytes = MessagePackSerializer.Serialize(original, cancellationToken: TestContext.Current.CancellationToken);

    var legacyRead = MessagePackSerializer.Deserialize<LegacyChatResponseHubPayload>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("User closed the chat window.", legacyRead.Message);
    Assert.Equal(original.SessionId, legacyRead.SessionId);
  }

  [Fact]
  public void ChatResponseHubDto_WindowClose_RoundTripsWithItsKind()
  {
    var original = new ChatResponseHubDto(
      Guid.NewGuid(),
      42,
      "User closed the chat window.",
      "DESKTOP-ABC",
      "conn1",
      DateTimeOffset.Now,
      ChatMessageKind.WindowClose);

    var bytes = MessagePackSerializer.Serialize(original, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<ChatResponseHubDto>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(ChatMessageKind.WindowClose, restored.Kind);
  }

  /// <summary>
  /// The viewer-facing message shape as it existed before the kind member was added: same keys, no Kind.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyChatMessageHubPayload(
    Guid DeviceId,
    Guid SessionId,
    string Message,
    string SenderName,
    string SenderEmail,
    int TargetSystemSession,
    int TargetProcessId,
    DateTimeOffset Timestamp,
    string ViewerConnectionId);

  /// <summary>
  /// The viewer-facing response shape as it existed before the kind member was added: same keys, no Kind.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyChatResponseHubPayload(
    Guid SessionId,
    int DesktopSessionProcessId,
    string Message,
    string SenderUsername,
    string ViewerConnectionId,
    DateTimeOffset Timestamp);
}

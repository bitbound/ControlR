using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Api.Contracts.Enums;
using MessagePack;
using PolyType;
using PolyType.ReflectionProvider;
using Xunit;
using NbMessagePackSerializer = Nerdbank.MessagePack.MessagePackSerializer;

namespace ControlR.Libraries.Ipc.Tests;

/// <summary>
/// The agent &lt;-&gt; desktop IPC boundary is Nerdbank.MessagePack over named pipes, and it writes every member
/// keyed by its name. A peer that predates <see cref="ChatMessageKind"/> sends a payload with no Kind key at all,
/// so the member must be optional on the way in and must land on <see cref="ChatMessageKind.TextSend"/>. If it
/// landed on any other value, every message from an older peer would render as a system line.
/// </summary>
public class ChatMessageKindIpcSerializationTests
{
  [Fact]
  public void ChatMessageIpcDto_FromLegacyPeerWithoutKind_ReadsBackAsTextSend()
  {
    var serializer = new NbMessagePackSerializer();
    var legacy = new LegacyChatMessageIpcPayload(
      SessionId: Guid.NewGuid(),
      Message: "an old viewer message",
      SenderName: "jared",
      SenderEmail: "jared@example.com",
      TargetSystemSession: 1,
      TargetProcessId: 42,
      ViewerConnectionId: "conn1",
      Timestamp: DateTimeOffset.Now);

    var restored = Deserialize<ChatMessageIpcDto>(serializer, Serialize(serializer, legacy));

    Assert.Equal(ChatMessageKind.TextSend, restored.Kind);
    Assert.Equal("an old viewer message", restored.Message);
  }

  [Fact]
  public void ChatMessageIpcDto_TextSend_RoundTripsWithItsKind()
  {
    var serializer = new NbMessagePackSerializer();
    var original = new ChatMessageIpcDto(
      Guid.NewGuid(),
      "plain text",
      "jared",
      "jared@example.com",
      1,
      42,
      "conn1",
      DateTimeOffset.Now,
      ChatMessageKind.TextSend);

    var restored = Deserialize<ChatMessageIpcDto>(serializer, Serialize(serializer, original));

    Assert.Equal(ChatMessageKind.TextSend, restored.Kind);
  }

  [Fact]
  public void ChatResponseIpcDto_FromLegacyPeerWithoutKind_ReadsBackAsTextSend()
  {
    var serializer = new NbMessagePackSerializer();
    var legacy = new LegacyChatResponseIpcPayload(
      SessionId: Guid.NewGuid(),
      DesktopUiProcessId: 42,
      Message: "an old desktop reply",
      SenderUsername: "DESKTOP-ABC",
      ViewerConnectionId: "conn1",
      Timestamp: DateTimeOffset.Now);

    var restored = Deserialize<ChatResponseIpcDto>(serializer, Serialize(serializer, legacy));

    Assert.Equal(ChatMessageKind.TextSend, restored.Kind);
    Assert.Equal("an old desktop reply", restored.Message);
  }

  [Fact]
  public void ChatResponseIpcDto_WindowClose_RoundTripsWithItsKind()
  {
    var serializer = new NbMessagePackSerializer();
    var original = new ChatResponseIpcDto(
      Guid.NewGuid(),
      42,
      "User closed the chat window.",
      "DESKTOP-ABC",
      "conn1",
      DateTimeOffset.Now,
      ChatMessageKind.WindowClose);

    var restored = Deserialize<ChatResponseIpcDto>(serializer, Serialize(serializer, original));

    Assert.Equal(ChatMessageKind.WindowClose, restored.Kind);
  }

  /// <summary>
  /// Deserializes with the same serializer instance the payload was written with, so the test exercises the
  /// formatter's own member-name reading rather than a hand-decoded buffer.
  /// </summary>
  private static T Deserialize<T>(NbMessagePackSerializer serializer, byte[] bytes) =>
    serializer.Deserialize(bytes, GetShape<T>(), TestContext.Current.CancellationToken)
    ?? throw new InvalidOperationException($"The IPC payload did not deserialize into {typeof(T)}.");

  /// <summary>
  /// Nerdbank.MessagePack needs an explicit shape for payload types this test assembly does not annotate, because
  /// the shape-less overloads only accept types whose shapes were generated into the compiling assembly.
  /// </summary>
  private static ITypeShape<T> GetShape<T>() =>
    ReflectionTypeShapeProvider.Default.GetTypeShape(typeof(T)) as ITypeShape<T>
    ?? throw new InvalidOperationException($"No MessagePack shape is available for {typeof(T)}.");

  private static byte[] Serialize<T>(NbMessagePackSerializer serializer, T value) =>
    serializer.Serialize(value, GetShape<T>(), TestContext.Current.CancellationToken);

  /// <summary>
  /// The desktop-facing message shape as it existed before the kind member was added: same keys, no Kind.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyChatMessageIpcPayload(
    Guid SessionId,
    string Message,
    string SenderName,
    string SenderEmail,
    int TargetSystemSession,
    int TargetProcessId,
    string ViewerConnectionId,
    DateTimeOffset Timestamp);

  /// <summary>
  /// The agent-facing response shape as it existed before the kind member was added: same keys, no Kind.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyChatResponseIpcPayload(
    Guid SessionId,
    int DesktopUiProcessId,
    string Message,
    string SenderUsername,
    string ViewerConnectionId,
    DateTimeOffset Timestamp);
}

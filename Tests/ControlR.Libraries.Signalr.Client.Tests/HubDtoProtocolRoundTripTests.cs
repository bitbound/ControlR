using System.Buffers;
using System.Collections;
using System.Reflection;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos.PwshCommandCompletions;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Signalr.Client.Tests;

/// <summary>
/// Round-trips representative hub DTOs through real <see cref="JsonHubProtocol" /> and
/// <see cref="MessagePackHubProtocol" /> instances, so a shape the MessagePack protocol cannot
/// decode is caught here rather than at the handshake of a live connection.
/// </summary>
public class HubDtoProtocolRoundTripTests
{
  private static readonly IHubProtocol _jsonProtocol = new JsonHubProtocol(
    Options.Create(new JsonHubProtocolOptions
    {
      PayloadSerializerOptions = { PropertyNameCaseInsensitive = true }
    }));

  private static readonly HashSet<Type> _leafTypes =
  [
    typeof(string),
    typeof(bool),
    typeof(byte),
    typeof(sbyte),
    typeof(short),
    typeof(ushort),
    typeof(int),
    typeof(uint),
    typeof(long),
    typeof(ulong),
    typeof(float),
    typeof(double),
    typeof(decimal),
    typeof(char),
    typeof(Guid),
    typeof(DateTime),
    typeof(DateTimeOffset),
    typeof(TimeSpan),
    typeof(DateOnly),
    typeof(TimeOnly),
    typeof(Uri),
    typeof(Version),
  ];

  private static readonly IHubProtocol _messagePackProtocol = new MessagePackHubProtocol();

  public static TheoryData<Type> RepresentativeTypes => new()
  {
    typeof(RemoteControlSessionRequestDto),
    typeof(VncSessionRequestDto),
    typeof(ChatMessageHubDto),
    typeof(ChatResponseHubDto),
    typeof(TerminalOutputDto),
    typeof(PwshCompletionMatch),
    typeof(PwshCompletionsRequestDto),
    typeof(PwshCompletionsResponseDto),
    typeof(GetPathSegmentsHubDto),
    typeof(DeviceAccessPermissionsDto),
    typeof(RemoteControlEventWrapper),
    typeof(RemoteControlCommandWrapper),
    typeof(HubResult),
    typeof(HubResult<DeviceSummaryDto>),
    typeof(ActiveDesktopSessionsResponseDto),
    typeof(FileUploadHubDto),
    typeof(FileArchiveDownloadHubDto),
    typeof(PathSegmentsResponseDto),
  };

  [Theory]
  [MemberData(nameof(RepresentativeTypes))]
  public void EveryRepresentativeTypeRoundTripsThroughBothProtocols(Type dtoType)
  {
    var original = CreateRepresentative(dtoType);

    var jsonInvocation = ExtractPayload(RoundTrip(_jsonProtocol, Invocation(original), dtoType));
    var messagePackInvocation = ExtractPayload(RoundTrip(_messagePackProtocol, Invocation(original), dtoType));
    AssertPayloadEqual(original, jsonInvocation, $"{dtoType.Name} json invocation");
    AssertPayloadEqual(jsonInvocation, messagePackInvocation, $"{dtoType.Name} invocation json vs messagepack");

    var jsonCompletion = ExtractPayload(RoundTrip(_jsonProtocol, Completion(original), dtoType));
    var messagePackCompletion = ExtractPayload(RoundTrip(_messagePackProtocol, Completion(original), dtoType));
    AssertPayloadEqual(original, jsonCompletion, $"{dtoType.Name} json completion");
    AssertPayloadEqual(jsonCompletion, messagePackCompletion, $"{dtoType.Name} completion json vs messagepack");
  }

  private static void AssertPayloadEqual(object? expected, object? actual, string path)
  {
    if (expected is null || actual is null)
    {
      Assert.True(
        expected is null && actual is null,
        $"{path}: expected '{expected?.GetType().Name ?? "null"}' but got '{actual?.GetType().Name ?? "null"}'.");
      return;
    }

    if (expected.GetType().IsEnum || _leafTypes.Contains(expected.GetType()))
    {
      Assert.True(object.Equals(expected, actual), $"{path}: expected '{expected}' but got '{actual}'.");
      return;
    }

    if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
    {
      Assert.True(expectedBytes.AsSpan().SequenceEqual(actualBytes), $"{path}: binary payloads differ.");
      return;
    }

    if (expected is IEnumerable expectedItems && actual is IEnumerable actualItems)
    {
      var actualList = actualItems.Cast<object?>().ToArray();
      var index = 0;

      foreach (var item in expectedItems)
      {
        Assert.True(
          index < actualList.Length,
          $"{path}: expected at least {index + 1} items but got {actualList.Length}.");
        AssertPayloadEqual(item, actualList[index], $"{path}[{index}]");
        index++;
      }

      Assert.True(index == actualList.Length, $"{path}: expected {index} items but got {actualList.Length}.");
      return;
    }

    Assert.True(
      expected.GetType() == actual.GetType(),
      $"{path}: expected type '{expected.GetType().FullName}' but got '{actual.GetType().FullName}'.");

    foreach (var property in expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      AssertPayloadEqual(property.GetValue(expected), property.GetValue(actual), $"{path}.{property.Name}");
    }
  }

  private static HubMessage Completion(object payload) =>
    new CompletionMessage("1", error: null, result: payload, hasResult: true);

  private static object CreateRepresentative(Type dtoType)
  {
    if (dtoType == typeof(RemoteControlSessionRequestDto))
    {
      return new RemoteControlSessionRequestDto(
        SessionId: Guid.NewGuid(),
        WebsocketUri: new Uri("wss://relay.example.com/remote-control"),
        TargetSystemSession: 1,
        TargetProcessId: 4242,
        DeviceId: Guid.NewGuid(),
        NotifyUserOnSessionStart: false,
        RequireConsent: true)
      {
        ViewerConnectionId = "conn-1",
        ViewerName = "jared",
        ViewerSupportsRemoteControlDtoHub = true,
      };
    }

    if (dtoType == typeof(VncSessionRequestDto))
    {
      return new VncSessionRequestDto(
        SessionId: Guid.NewGuid(),
        WebsocketUri: new Uri("wss://relay.example.com/vnc"),
        ViewerConnectionId: "conn-2",
        DeviceId: Guid.NewGuid(),
        NotifyUserOnSessionStart: true,
        Port: 5900)
      {
        ViewerName = "jared",
      };
    }

    if (dtoType == typeof(ChatMessageHubDto))
    {
      return new ChatMessageHubDto(
        DeviceId: Guid.NewGuid(),
        SessionId: Guid.NewGuid(),
        Message: "closing the window",
        SenderName: "jared",
        SenderEmail: "jared@example.com",
        TargetSystemSession: 1,
        TargetProcessId: 42,
        Timestamp: new DateTimeOffset(2026, 10, 23, 9, 30, 0, TimeSpan.FromHours(-5)),
        ViewerConnectionId: "conn-3",
        Kind: ChatMessageKind.WindowClose);
    }

    if (dtoType == typeof(ChatResponseHubDto))
    {
      return new ChatResponseHubDto(
        SessionId: Guid.NewGuid(),
        DesktopSessionProcessId: 43,
        Message: "window closed",
        SenderUsername: "DESKTOP-ABC",
        ViewerConnectionId: "conn-4",
        Timestamp: new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.FromHours(-5)),
        Kind: ChatMessageKind.WindowClose);
    }

    if (dtoType == typeof(TerminalOutputDto))
    {
      return new TerminalOutputDto(
        TerminalId: Guid.NewGuid(),
        Output: "total 0\r\n",
        OutputKind: TerminalOutputKind.StandardOutput,
        Timestamp: new DateTimeOffset(2026, 10, 23, 11, 15, 30, TimeSpan.FromHours(-5)));
    }

    if (dtoType == typeof(PwshCompletionMatch))
    {
      return new PwshCompletionMatch(
        CompletionText: "Get-ChildItem",
        ListItemText: "Get-ChildItem",
        MatchType: PwshCompletionMatchType.Command,
        ToolTip: "Lists items");
    }

    if (dtoType == typeof(PwshCompletionsRequestDto))
    {
      return new PwshCompletionsRequestDto(
        DeviceId: Guid.NewGuid(),
        TerminalId: Guid.NewGuid(),
        LastCompletionInput: "Get-Ch",
        LastCursorIndex: 6,
        ViewerConnectionId: "conn-5",
        Forward: true,
        Page: 1,
        PageSize: 25);
    }

    if (dtoType == typeof(PwshCompletionsResponseDto))
    {
      return new PwshCompletionsResponseDto(
        ReplacementIndex: 0,
        ReplacementLength: 5,
        CompletionMatches:
        [
          new PwshCompletionMatch(
            CompletionText: "Get-ChildItem",
            ListItemText: "Get-ChildItem",
            MatchType: PwshCompletionMatchType.Command,
            ToolTip: "Lists items"),
        ],
        HasMorePages: true,
        TotalCount: 12,
        CurrentPage: 1);
    }

    if (dtoType == typeof(GetPathSegmentsHubDto))
    {
      return new GetPathSegmentsHubDto { TargetPath = "/var/log" };
    }

    if (dtoType == typeof(DeviceAccessPermissionsDto))
    {
      return new DeviceAccessPermissionsDto(
        CanViewOverview: true,
        CanUseRemoteControl: true,
        CanUseTerminal: false,
        CanUseChat: true,
        CanReadFileSystem: false,
        CanReadLogs: true,
        CanUseVncRelay: false,
        CanInteractWithRemoteControl: true,
        CanBlockRemoteInput: false,
        CanReadClipboard: true,
        CanWriteClipboard: false,
        CanSendCtrlAltDel: true,
        CanTogglePrivacyScreen: false);
    }

    if (dtoType == typeof(RemoteControlEventWrapper))
    {
      return new RemoteControlEventWrapper
      {
        DtoType = RemoteControlEventDtoType.ClipboardText,
        Payload = [0x01, 0x02, 0x03, 0xFF],
        SendTimestamp = 1234567890L,
      };
    }

    if (dtoType == typeof(RemoteControlCommandWrapper))
    {
      return new RemoteControlCommandWrapper
      {
        DtoType = RemoteControlCommandDtoType.ChangeDisplays,
        Payload = [0xFE, 0x00, 0x7F, 0x10],
        SendTimestamp = 9876543210L,
      };
    }

    if (dtoType == typeof(HubResult))
    {
      return HubResult.Fail("permission denied", OperationFailureCode.PermissionDenied);
    }

    if (dtoType == typeof(HubResult<DeviceSummaryDto>))
    {
      return HubResult.Ok(new DeviceSummaryDto(
        Id: Guid.NewGuid(),
        LastSeen: new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.FromHours(-5)),
        AgentVersion: "1.2.3"));
    }

    if (dtoType == typeof(ActiveDesktopSessionsResponseDto))
    {
      return new ActiveDesktopSessionsResponseDto(
      [
        new DesktopSession
        {
          AreRemoteControlPermissionsGranted = true,
          DesktopName = "Console",
          Name = "Session 1",
          ProcessId = 4242,
          SystemSessionId = 1,
          Type = DesktopSessionType.Console,
          Username = "jared",
        },
      ]);
    }

    if (dtoType == typeof(FileUploadHubDto))
    {
      return new FileUploadHubDto(Guid.NewGuid(), "/tmp/incoming", "file.bin", 4096L, Overwrite: true);
    }

    if (dtoType == typeof(FileArchiveDownloadHubDto))
    {
      return new FileArchiveDownloadHubDto(Guid.NewGuid(), "archive.zip", ["/tmp/a", "/tmp/b"]);
    }

    if (dtoType == typeof(PathSegmentsResponseDto))
    {
      return new PathSegmentsResponseDto
      {
        ErrorMessage = null,
        PathExists = true,
        PathSegments = ["~", "logs", "agent.log"],
        PathSeparator = "/",
        Success = true,
      };
    }

    throw new InvalidOperationException($"No representative instance is defined for '{dtoType.FullName}'.");
  }

  private static object? ExtractPayload(HubMessage message) => message switch
  {
    InvocationMessage invocation => invocation.Arguments[0],
    CompletionMessage completion => completion.Result,
    _ => throw new InvalidOperationException($"Unexpected parsed message type: '{message.GetType().FullName}'."),
  };

  private static HubMessage Invocation(object payload) =>
    new InvocationMessage("1", "RoundTrip", [payload]);

  private static HubMessage RoundTrip(IHubProtocol protocol, HubMessage message, Type payloadType)
  {
    var writer = new ArrayBufferWriter<byte>();
    protocol.WriteMessage(message, writer);

    var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
    var binder = new SingleTypeInvocationBinder(payloadType);

    Assert.True(
      protocol.TryParseMessage(ref sequence, binder, out var parsed),
      $"'{protocol.Name}' could not parse the '{message.GetType().Name}' it wrote for '{payloadType.Name}'.");

    return parsed!;
  }

  private sealed class SingleTypeInvocationBinder(Type payloadType) : IInvocationBinder
  {
    public IReadOnlyList<Type> GetParameterTypes(string methodName) => [payloadType];

    public Type GetReturnType(string invocationId) => payloadType;

    public Type GetStreamItemType(string streamId) => payloadType;
  }
}
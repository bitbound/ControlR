using ControlR.Shared.Enums;
using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record TerminalSessionRequestResult([property: MsgPackKey] TerminalSessionKind SessionKind);
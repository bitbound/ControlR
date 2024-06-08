using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record TerminalSessionRequestResult([property: MsgPackKey] TerminalSessionKind SessionKind);
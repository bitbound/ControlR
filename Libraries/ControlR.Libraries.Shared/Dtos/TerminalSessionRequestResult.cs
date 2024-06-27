using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record TerminalSessionRequestResult([property: MsgPackKey] TerminalSessionKind SessionKind);
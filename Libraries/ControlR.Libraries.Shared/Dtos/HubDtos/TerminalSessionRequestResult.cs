using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record TerminalSessionRequestResult([property: MsgPackKey] TerminalSessionKind SessionKind);
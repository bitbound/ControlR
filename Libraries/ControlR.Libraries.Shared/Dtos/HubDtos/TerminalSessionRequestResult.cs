using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record TerminalSessionRequestResult([property: Key(0)] TerminalSessionKind SessionKind);
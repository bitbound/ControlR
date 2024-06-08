using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record CloseTerminalRequestDto([property: MsgPackKey] Guid TerminalId);
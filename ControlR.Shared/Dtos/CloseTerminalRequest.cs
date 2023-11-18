using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record CloseTerminalRequest([property: MsgPackKey] Guid TerminalId);
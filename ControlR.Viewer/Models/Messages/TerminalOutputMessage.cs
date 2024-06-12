using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Viewer.Models.Messages;

[MessagePackObject]
public record TerminalOutputMessage([property: MsgPackKey] TerminalOutputDto OutputDto);
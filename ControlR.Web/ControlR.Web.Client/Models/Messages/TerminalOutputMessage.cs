using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Web.Client.Models.Messages;

[MessagePackObject]
public record TerminalOutputMessage([property: MsgPackKey] TerminalOutputDto OutputDto);
using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class TerminalSessionRequest(Guid terminalId)
{
    [MsgPackKey]
    public Guid TerminalId { get; init; } = terminalId;
}
using ControlR.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Agent.Models.IpcDtos;

[MessagePackObject]
[method: JsonConstructor]
[method: SerializationConstructor]
public class DesktopChangeDto(string desktopName)
{
    [MsgPackKey]
    public string DesktopName { get; set; } = desktopName;
}
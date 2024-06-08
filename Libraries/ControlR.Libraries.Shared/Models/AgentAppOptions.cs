using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
    public const string SectionKey = "AppOptions";

    [MsgPackKey]
    public List<string> AuthorizedKeys { get; set; } = [];

    [MsgPackKey]
    public string DeviceId { get; set; } = string.Empty;

    [MsgPackKey]
    public string? ServerUri { get; set; }
}
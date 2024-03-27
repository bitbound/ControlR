using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
    public const string ConfigurationKey = "AppOptions";

    [MsgPackKey]
    public List<string> AuthorizedKeys { get; set; } = [];

    [MsgPackKey]
    public string DeviceId { get; set; } = string.Empty;

    [MsgPackKey]
    public string? ServerUri { get; set; }
}
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
    public const string SectionKey = "AppOptions";

    [Obsolete("Use AuthorizedKeys2 instead.")]
    [MsgPackKey]
    public ConcurrentList<string> AuthorizedKeys { get; set; } = [];

    [MsgPackKey]
    public ConcurrentList<AuthorizedKeyDto> AuthorizedKeys2 { get; set; } = [];

    [MsgPackKey]
    public string DeviceId { get; set; } = string.Empty;

    [MsgPackKey]
    public string? ServerUri { get; set; }
}
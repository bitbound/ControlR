using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
    public const string SectionKey = "AppOptions";
    
    [MsgPackKey]
    public Guid DeviceId { get; set; }

    [MsgPackKey]
    public Uri? ServerUri { get; set; }
}
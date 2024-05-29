namespace ControlR.Shared.Models;

[MessagePackObject]
public class AgentRuntimeSettings
{
    [MsgPackKey]
    public bool? GitHubEnabled { get; set; }
}

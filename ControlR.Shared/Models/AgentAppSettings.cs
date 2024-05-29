namespace ControlR.Shared.Models;

[MessagePackObject]
public class AgentAppSettings
{
    [MsgPackKey]
    public AgentAppOptions AppOptions { get; init; } = new();
}
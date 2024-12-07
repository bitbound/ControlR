namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppSettings
{
    [Key(nameof(AppOptions))]
    public AgentAppOptions AppOptions { get; set; } = new();
}
namespace ControlR.Agent.Models;

internal class AppOptions
{
    public List<string> AuthorizedKeys { get; set; } = [];
    public string DeviceId { get; set; } = string.Empty;
}
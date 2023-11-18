namespace ControlR.Agent.Models;

internal class AppOptions
{
    public List<string> AuthorizedKeys { get; set; } = [];
    public bool? AutoRunVnc { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? ServerUri { get; set; }
    public int? VncPort { get; set; }
}
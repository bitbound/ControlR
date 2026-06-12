namespace ControlR.Agent.Shared.Options;

public class AgentAppOptions
{
  public const string SectionKey = "AppOptions";

  public Guid DeviceId { get; set; }
  public string? PrivateKey { get; set; }
  public Uri? ServerUri { get; set; }
  public Guid TenantId { get; set; }
}

namespace ControlR.Agent.Common.Options;

public class AgentAppOptions
{
  public const string SectionKey = "AppOptions";

  public Guid DeviceId { get; set; }

  public Uri? ServerUri { get; set; }
  public Guid TenantId { get; set; }
}
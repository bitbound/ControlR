namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
  public const string SectionKey = "AppOptions";

  [Key(0)]
  public Guid DeviceId { get; set; }

  [Key(1)]
  public Uri? ServerUri { get; set; }

  [Key(2)]
  public Guid TenantId { get; set; }
}
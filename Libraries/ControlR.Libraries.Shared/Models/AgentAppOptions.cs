using ControlR.Libraries.Shared.Constants;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject(keyAsPropertyName: true)]
public class AgentAppOptions
{
  public const string SectionKey = "AppOptions";

  public Guid DeviceId { get; set; }

  public Uri? ServerUri { get; set; }
  public Guid TenantId { get; set; }
  public int? VncPort { get; set; }
}
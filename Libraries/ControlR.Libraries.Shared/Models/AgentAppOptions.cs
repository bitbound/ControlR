namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class AgentAppOptions
{
  public const string SectionKey = "AppOptions";

  [MsgPackKey]
  public Guid DeviceId { get; set; }

  [MsgPackKey]
  public Uri? ServerUri { get; set; }

  [MsgPackKey]
  public Guid[]? TagIds { get; set; }

  [MsgPackKey]
  public Guid TenantId { get; set; }
}
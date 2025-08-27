namespace ControlR.Agent.Common.Options;

public class AgentAppOptions
{
  public const string SectionKey = "AppOptions";

  public Guid DeviceId { get; set; }
  
  // Maximum number of DTO items (e.g. FileSystemEntryDto) to send per streaming chunk.
  // If null or less than 1, a default of 100 will be used by consumers.
  public int? HubDtoChunkSize { get; set; }

  public Uri? ServerUri { get; set; }
  public Guid TenantId { get; set; }
  public int? VncPort { get; set; }
}
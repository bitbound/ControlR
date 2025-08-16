namespace ControlR.Libraries.DevicesCommon.Options;
public class InstanceOptions
{
  public const string SectionKey = "InstanceOptions";
  private readonly string? _instanceId;

  public string? InstanceId
  {
    get => _instanceId;
    init => _instanceId = value?.SanitizeForFileSystem();
  }
}

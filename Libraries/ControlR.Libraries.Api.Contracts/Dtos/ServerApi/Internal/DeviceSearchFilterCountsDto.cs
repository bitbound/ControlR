namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public class DeviceSearchFilterCountsDto
{
  public int OfflineDevices { get; set; }
  public int OnlineDevices { get; set; }
  public int TaggedDevices { get; set; }
  public int UntaggedDevices { get; set; }
}

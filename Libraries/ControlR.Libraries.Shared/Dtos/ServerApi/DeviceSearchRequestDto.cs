namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceSearchRequestDto
{
  public List<DeviceColumnFilter>? FilterDefinitions { get; set; }
  public bool HideOfflineDevices { get; set; }
  public int Page { get; set; }
  public int PageSize { get; set; }
  public string? SearchText { get; set; }
  public List<DeviceColumnSort>? SortDefinitions { get; set; }
  public List<Guid>? TagIds { get; set; }
}


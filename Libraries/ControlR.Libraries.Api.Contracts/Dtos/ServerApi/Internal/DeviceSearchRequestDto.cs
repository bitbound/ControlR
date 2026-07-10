using ControlR.Libraries.Api.Contracts.FilterSort;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public class DeviceSearchRequestDto
{
  public List<DeviceColumnFilter>? FilterDefinitions { get; set; }
  public bool HideOfflineDevices { get; set; }
  public bool IncludeUntaggedDevices { get; set; }
  public int Page { get; set; }
  public int PageSize { get; set; }
  public string? SearchText { get; set; }
  public List<DeviceColumnSort>? SortDefinitions { get; set; }
  public List<Guid>? TagIds { get; set; }
}

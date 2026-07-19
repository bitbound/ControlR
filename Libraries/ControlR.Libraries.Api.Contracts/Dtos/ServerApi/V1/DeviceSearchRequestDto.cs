using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.FilterSort;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public class DeviceSearchRequestDto
{
  public List<DeviceColumnFilter>? FilterDefinitions { get; set; }
  public bool HideOfflineDevices { get; set; }
  public bool IncludeUntaggedDevices { get; set; }
  [Range(0, int.MaxValue)]
  public int Page { get; set; }
  [Range(1, int.MaxValue)]
  public int PageSize { get; set; }
  public string? SearchText { get; set; }
  public List<DeviceColumnSort>? SortDefinitions { get; set; }
  public List<Guid>? TagIds { get; set; }
}

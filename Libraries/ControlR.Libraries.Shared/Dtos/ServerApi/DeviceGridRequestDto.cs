using ControlR.Libraries.Shared.Enums;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceGridRequestDto
{
    public string? SearchText { get; set; }
    public bool HideOfflineDevices { get; set; }
    public List<Guid>? TagIds { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<DeviceColumnSort>? SortDefinitions { get; set; }
}

public class DeviceColumnSort
{
    public string PropertyName { get; set; } = string.Empty;
    public bool Descending { get; set; }
    public int SortOrder { get; set; }
}

public class DeviceGridResponseDto
{
    public List<DeviceDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
}

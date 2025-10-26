namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceColumnSort
{
  public bool Descending { get; set; }
  public string? PropertyName { get; set; }
  public int SortOrder { get; set; }
}
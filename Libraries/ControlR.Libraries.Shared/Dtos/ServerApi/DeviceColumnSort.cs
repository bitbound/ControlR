namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceColumnSort
{
  public string? PropertyName { get; set; }
  public bool Descending { get; set; }
  public int SortOrder { get; set; }
}
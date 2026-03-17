namespace ControlR.Web.Client.Models;

public static class HistoryEntryStates
{
  public static string CreateDeviceAccess(bool canGoBack = true)
  {
    return JsonSerializer.Serialize(new DeviceAccessHistoryEntry
    {
      CanGoBack = canGoBack
    });
  }
}

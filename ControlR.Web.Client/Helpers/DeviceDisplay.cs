namespace ControlR.Web.Client.Helpers;

public static class DeviceDisplay
{
  public static string GetAliasDisplay(InternalDtos.DeviceResponseDto device) =>
    string.IsNullOrWhiteSpace(device.Alias) ? "—" : device.Alias;

  public static string GetCustomerDisplay(InternalDtos.DeviceResponseDto device) =>
    string.IsNullOrWhiteSpace(device.CustomerName) ? "—" : device.CustomerName;

  public static string GetFullDisplayName(InternalDtos.DeviceResponseDto device) =>
    $"{device.Name}  (Customer: {GetCustomerDisplay(device)}  |  Alias: {GetAliasDisplay(device)}  |  Device ID: {GetIdDisplay(device)})";

  public static string GetIdDisplay(InternalDtos.DeviceResponseDto device) =>
    device.Id.ToString()[..8] + "...";
}

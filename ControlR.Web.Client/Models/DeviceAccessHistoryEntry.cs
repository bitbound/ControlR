namespace ControlR.Web.Client.Models;

public sealed record DeviceAccessHistoryEntry
{
  public const string EntryTypeName = "device-access";

  public string EntryType { get; } = EntryTypeName;
  public bool CanGoBack { get; init; }
}

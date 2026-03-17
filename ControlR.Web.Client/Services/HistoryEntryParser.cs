using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Client.Services;

public interface IHistoryEntryParser
{
  bool TryParseForDeviceAccess(string? historyEntryState, [NotNullWhen(true)] out DeviceAccessHistoryEntry? historyEntry);
}

internal sealed class HistoryEntryParser : IHistoryEntryParser
{

  public bool TryParseForDeviceAccess(string? historyEntryState, [NotNullWhen(true)] out DeviceAccessHistoryEntry? historyEntry)
  {
    historyEntry = null;

    if (string.IsNullOrWhiteSpace(historyEntryState))
    {
      return false;
    }

    try
    {
      historyEntry = JsonSerializer.Deserialize<DeviceAccessHistoryEntry>(historyEntryState);
      if (historyEntry?.EntryType != DeviceAccessHistoryEntry.EntryTypeName)
      {
        return false;
      }

      return true;
    }
    catch (Exception)
    {
      return false;
    }
  }
}

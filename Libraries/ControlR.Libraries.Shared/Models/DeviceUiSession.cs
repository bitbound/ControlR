namespace ControlR.Libraries.Shared.Models;

public enum UiSessionType
{
  Console = 0,
  Rdp = 1
}

[MessagePackObject(keyAsPropertyName: true)]
public class DeviceUiSession
{
  public string Name { get; set; } = string.Empty;
  public int ProcessId { get; set; }
  public int SystemSessionId { get; set; }
  public UiSessionType Type { get; set; }

  public string Username { get; set; } = string.Empty;
}
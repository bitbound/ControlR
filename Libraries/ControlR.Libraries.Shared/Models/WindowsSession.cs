namespace ControlR.Libraries.Shared.Models;

public enum WindowsSessionType
{
  Console = 0,
  Rdp = 1
}

[MessagePackObject]
public class WindowsSession
{
  [Key(nameof(Id))] 
  public uint Id { get; set; }

  [Key(nameof(Name))] 
  public string Name { get; set; } = string.Empty;

  [Key(nameof(Type))] 
  public WindowsSessionType Type { get; set; }

  [Key(nameof(Username))] 
  public string Username { get; set; } = string.Empty;
}
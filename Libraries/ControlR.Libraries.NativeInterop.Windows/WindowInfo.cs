using System.Drawing;

namespace ControlR.Libraries.DevicesNative.Services.Windows;
public record WindowInfo(
  nint WindowHandle,
  int ProcessId,
  string ProcessName,
  string Title,
  int X,
  int Y,
  int Width,
  int Height,
  int Zorder)
{
  public Size Size { get; } = new(Width, Height);

  public override string ToString()
    {
        return $"Process Name: {ProcessName}, PID: {ProcessId}, Title: {Title} ({X}, {Y}, {Width}, {Height})";
    }
}

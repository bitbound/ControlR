using System.Drawing;
using System.Numerics;

namespace ControlR.DesktopClient.Common.Models;

public class DisplayInfo
{
  public required string DeviceName { get; init; }
  public string DisplayName { get; set; } = string.Empty;
  public int Index { get; set; }
  public bool IsPrimary { get; init; }
  public Rectangle MonitorArea { get; init; }
  public double ScaleFactor { get; set; } = 1;
  public Rectangle WorkArea { get; set; }
}

using System.Drawing;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Windows.Helpers;

internal static class DisplayEnumHelperWindows
{
  private const int Cchdevicename = 32;

  private delegate bool EnumMonitorsDelegate(nint hMonitor, nint hdcMonitor, ref Rect lprcMonitor, nint dwData);

  public static List<DisplayInfo> GetDisplays()
  {
    var displays = new List<DisplayInfo>();

    EnumDisplayMonitors(
      nint.Zero,
      nint.Zero,
      (nint hMonitor, nint _, ref Rect _, nint _) =>
      {
        var mi = new MonitorInfoEx();
        mi.Size = Marshal.SizeOf(mi);
        var success = GetMonitorInfo(hMonitor, ref mi);
        if (!success)
        {
          return true;
        }

        var physicalBounds = new Rectangle(
          mi.Monitor.Left,
          mi.Monitor.Top,
          mi.Monitor.Right - mi.Monitor.Left,
          mi.Monitor.Bottom - mi.Monitor.Top);

        var info = new DisplayInfo
        {
          DisplayName = $"Display {displays.Count + 1}",
          CapturePixelSize = new Size(physicalBounds.Width, physicalBounds.Height),
          LayoutBounds = physicalBounds,
          LayoutCoordinateSpace = DisplayLayoutCoordinateSpace.Physical,
          IsPrimary = mi.Flags > 0,
          DeviceName = mi.DeviceName,
          Index = displays.Count
        };

        displays.Add(info);

        return true;
      }, nint.Zero);

    return displays;
  }

  [DllImport("user32.dll")]
  private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, EnumMonitorsDelegate lpfnEnum, nint dwData);

  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
  private struct MonitorInfoEx
  {
    public int Size;
    public Rect Monitor;
    public Rect WorkArea;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Cchdevicename)]
    public string DeviceName;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct Rect
  {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
  }
}
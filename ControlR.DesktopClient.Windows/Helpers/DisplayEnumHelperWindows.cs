using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using ControlR.DesktopClient.Common.Models;

namespace ControlR.DesktopClient.Windows.Helpers;

internal static class DisplayEnumHelperWindows
{
  private const int Cchdevicename = 32;

  private delegate bool EnumMonitorsDelegate(nint hMonitor, nint hdcMonitor, ref Rect lprcMonitor, nint dwData);

  /// <summary>
  /// Returns each display paired with its physical pixel bounds in the global virtual screen.
  /// The physical bounds are not stored in <see cref="DisplayInfo"/> because a global physical
  /// origin is not universally knowable (e.g. Wayland/macOS mixed-DPI), but on Windows Win32
  /// reports them directly via <c>MONITORINFOEX.rcMonitor</c>.
  /// </summary>
  public static List<(DisplayInfo Display, Rectangle PhysicalBounds)> GetDisplays()
  {
    var displays = new List<(DisplayInfo, Rectangle)>();

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

        var scale = 1.0;

        var physicalBounds = new Rectangle(
          mi.Monitor.Left,
          mi.Monitor.Top,
          mi.Monitor.Right - mi.Monitor.Left,
          mi.Monitor.Bottom - mi.Monitor.Top);

        unsafe
        {
          var devMode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
          if (PInvoke.EnumDisplaySettings(mi.DeviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref devMode))
          {
            scale = devMode.dmLogPixels / 96.0;
          }
        }

        var logicalLeft = (int)Math.Round(physicalBounds.Left / scale);
        var logicalTop = (int)Math.Round(physicalBounds.Top / scale);
        var logicalWidth = (int)Math.Round(physicalBounds.Width / scale);
        var logicalHeight = (int)Math.Round(physicalBounds.Height / scale);

        var info = new DisplayInfo
        {
          DisplayName = $"Display {displays.Count + 1}",
          PhysicalSize = new Size(physicalBounds.Width, physicalBounds.Height),
          LogicalMonitorArea = new Rectangle(logicalLeft, logicalTop, logicalWidth, logicalHeight),
          IsPrimary = mi.Flags > 0,
          DeviceName = mi.DeviceName,
          Index = displays.Count,
          ScaleFactor = scale
        };

        displays.Add((info, physicalBounds));

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
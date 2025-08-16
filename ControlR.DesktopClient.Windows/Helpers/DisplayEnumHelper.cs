//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using ControlR.DesktopClient.Common.Models;

namespace ControlR.DesktopClient.Windows.Helpers;

internal static class DisplaysEnumerationHelper
{
  private const int Cchdevicename = 32;

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
        if (success)
        {
          var info = new DisplayInfo
          {
            ScreenSize = new Vector2(mi.Monitor.Right - mi.Monitor.Left, mi.Monitor.Bottom - mi.Monitor.Top),
            MonitorArea = new Rectangle(mi.Monitor.Left, mi.Monitor.Top, mi.Monitor.Right - mi.Monitor.Left,
              mi.Monitor.Bottom - mi.Monitor.Top),
            WorkArea = new Rectangle(mi.WorkArea.Left, mi.WorkArea.Top, mi.WorkArea.Right - mi.WorkArea.Left,
              mi.WorkArea.Bottom - mi.WorkArea.Top),
            IsPrimary = mi.Flags > 0,
            Hmon = hMonitor,
            DeviceName = mi.DeviceName
          };
          displays.Add(info);
        }

        return true;
      }, nint.Zero);

    unsafe
    {
      var devMode = new DEVMODEW
      {
        dmSize = (ushort)sizeof(DEVMODEW)
      };

      for (var i = 0; i < displays.Count; i++)
      {
        var display = displays[i];
        display.DisplayName = $"Screen {i}";

        if (PInvoke.EnumDisplaySettings(display.DeviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS,
              ref devMode))
        {
          display.ScaleFactor = devMode.dmLogPixels / 96.0;
        }
      }
    }

    return displays;
  }

  [DllImport("user32.dll")]
  private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, EnumMonitorsDelegate lpfnEnum, nint dwData);

  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  private static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

  private delegate bool EnumMonitorsDelegate(nint hMonitor, nint hdcMonitor, ref Rect lprcMonitor, nint dwData);

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
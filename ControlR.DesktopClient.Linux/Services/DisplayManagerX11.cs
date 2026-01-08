using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.DesktopClient.Linux.Services;

internal class DisplayManagerX11 : IDisplayManager
{
  private readonly Lock _displayLock = new();
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerX11> _logger;

  public DisplayManagerX11(ILogger<DisplayManagerX11> logger)
  {
    _logger = logger;
  }

  public async Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY)
  {
    var findResult = await TryFindDisplay(displayName);
    if (!findResult.IsSuccess)
    {
      return Point.Empty;
    }

    var bounds = findResult.Value.MonitorArea;
    var absoluteX = (int)(bounds.Left + bounds.Width * percentX);
    var absoluteY = (int)(bounds.Top + bounds.Height * percentY);

    return new Point(absoluteX, absoluteY);
  }

  public Task<ImmutableList<DisplayInfo>> GetDisplays()
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();

      var displayDtos = _displays
        .Values
        .ToImmutableList();

      return Task.FromResult(displayDtos);
    }
  }

  public async Task<DisplayInfo?> GetPrimaryDisplay()
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      return _displays.Values.FirstOrDefault(x => x.IsPrimary)
        ?? _displays.Values.FirstOrDefault();
    }
  }

  public async Task<Rectangle> GetVirtualScreenBounds()
  {
    try
    {
      lock (_displayLock)
      {
        EnsureDisplaysLoaded();
        if (_displays.Count == 0)
        {
          return Rectangle.Empty;
        }

        var minX = _displays.Values.Min(d => d.MonitorArea.Left);
        var minY = _displays.Values.Min(d => d.MonitorArea.Top);
        var maxX = _displays.Values.Max(d => d.MonitorArea.Right);
        var maxY = _displays.Values.Max(d => d.MonitorArea.Bottom);

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting virtual screen bounds.");
      // Return default screen bounds as fallback
      var xDisplay = LibX11.XOpenDisplay("");
      if (xDisplay != nint.Zero)
      {
        try
        {
          var screenNumber = LibX11.XDefaultScreen(xDisplay);
          var width = LibX11.XDisplayWidth(xDisplay, screenNumber);
          var height = LibX11.XDisplayHeight(xDisplay, screenNumber);
          return new Rectangle(0, 0, width, height);
        }
        finally
        {
          LibX11.XCloseDisplay(xDisplay);
        }
      }
      return Rectangle.Empty;
    }
  }

  public Task ReloadDisplays()
  {
    lock (_displayLock)
    {
      ReloadDisplaysImpl();
    }
    return Task.CompletedTask;
  }

  public Task<Result> SetPrivacyScreen(bool isEnabled)
  {
    throw new PlatformNotSupportedException("Privacy screen is only supported on Windows.");
  }

  public Task<Result<DisplayInfo>> TryFindDisplay(string deviceName)
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      if (_displays.TryGetValue(deviceName, out var display))
      {
        return Task.FromResult(Result.Ok(display));
      }
      return Task.FromResult(Result.Fail<DisplayInfo>("Display not found."));
    }
  }

  private void EnsureDisplaysLoaded()
  {
    // Must be called within lock
    if (_displays.Count == 0)
    {
      ReloadDisplaysImpl();
    }
  }

  private void ReloadDisplaysImpl()
  {
    // Must be called within lock
    try
    {
      _displays.Clear();

      var xDisplay = LibX11.XOpenDisplay("");
      if (xDisplay == nint.Zero)
      {
        _logger.LogError("Failed to open X11 display.");
        return;
      }

      try
      {
        // Try to get monitors using XRandR
        var rootWindow = LibX11.XDefaultRootWindow(xDisplay);
        var monitorsPtr = LibXrandr.XRRGetMonitors(xDisplay, rootWindow, true, out var monitorCount);

        if (monitorsPtr != nint.Zero && monitorCount > 0)
        {
          // Use XRandR monitors
          for (var i = 0; i < monitorCount; i++)
          {
            var monitorPtr = monitorsPtr + i * Marshal.SizeOf<LibXrandr.XRRMonitorInfo>();
            var monitor = Marshal.PtrToStructure<LibXrandr.XRRMonitorInfo>(monitorPtr);

            var displayInfo = new DisplayInfo
            {
              DeviceName = i.ToString(),
              DisplayName = $"Display {i}",
              Index = i,
              MonitorArea = new Rectangle(monitor.x, monitor.y, monitor.width, monitor.height),
              WorkArea = new Rectangle(monitor.x, monitor.y, monitor.width, monitor.height),
              IsPrimary = monitor.primary,
              ScaleFactor = 1.0 // X11 doesn't provide easy access to scale factor
            };

            _displays[displayInfo.DeviceName] = displayInfo;
          }

          LibXrandr.XRRFreeMonitors(monitorsPtr);
        }
        else
        {
          // Fallback to basic X11 screen enumeration
          var screenCount = LibX11.XScreenCount(xDisplay);
          for (var i = 0; i < screenCount; i++)
          {
            var width = LibX11.XDisplayWidth(xDisplay, i);
            var height = LibX11.XDisplayHeight(xDisplay, i);

            var displayInfo = new DisplayInfo
            {
              DeviceName = i.ToString(),
              DisplayName = $"Screen {i}",
              Index = i,
              MonitorArea = new Rectangle(0, 0, width, height),
              WorkArea = new Rectangle(0, 0, width, height),
              IsPrimary = i == 0,
              ScaleFactor = 1.0
            };

            _displays[displayInfo.DeviceName] = displayInfo;
          }
        }
      }
      finally
      {
        LibX11.XCloseDisplay(xDisplay);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading display list.");
    }
  }
}
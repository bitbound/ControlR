using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
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

  public async Task<LogicalPoint> ConvertDisplayPercentToLogical(string displayName, double percentOfDisplayX, double percentOfDisplayY)
  {
    var findResult = await TryFindDisplay(displayName);
    if (!findResult.IsSuccess)
    {
      return default;
    }

    return DisplayCoordinateConverter
        .DisplayPercentToLogical(percentOfDisplayX, percentOfDisplayY, findResult.Value);
  }

  public async Task<PhysicalPoint> ConvertDisplayPercentToPhysical(string displayName, double percentOfDisplayX, double percentOfDisplayY)
  {
    var findResult = await TryFindDisplay(displayName);
    if (!findResult.IsSuccess)
    {
      return default;
    }

    return DisplayCoordinateConverter
        .DisplayPercentToPhysical(percentOfDisplayX, percentOfDisplayY, findResult.Value);
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

  public async Task<LogicalRect> GetVirtualScreenLogicalBounds()
  {
    try
    {
      lock (_displayLock)
      {
        EnsureDisplaysLoaded();
        if (_displays.Count == 0)
        {
          return default;
        }

        var minX = _displays.Values.Min(d => d.LogicalMonitorArea.Left);
        var minY = _displays.Values.Min(d => d.LogicalMonitorArea.Top);
        var maxX = _displays.Values.Max(d => d.LogicalMonitorArea.Right);
        var maxY = _displays.Values.Max(d => d.LogicalMonitorArea.Bottom);

        return new LogicalRect(minX, minY, maxX - minX, maxY - minY);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting virtual logical screen bounds.");
      return default;
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

            var monitorRect = new Rectangle(monitor.x, monitor.y, monitor.width, monitor.height);
            var displayInfo = new DisplayInfo
            {
              DeviceName = i.ToString(),
              DisplayName = $"Display {i + 1}",
              Index = i,
              PhysicalSize = new Size(monitorRect.Width, monitorRect.Height),
              LogicalMonitorArea = new Rectangle(monitorRect.Left, monitorRect.Top, monitorRect.Width, monitorRect.Height),
              IsPrimary = monitor.primary,
              ScaleFactor = 1.0
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

            var monitorRect = new Rectangle(0, 0, width, height);
            var displayInfo = new DisplayInfo
            {
              DeviceName = i.ToString(),
              DisplayName = $"Display {i + 1}",
              Index = i,
              PhysicalSize = new Size(monitorRect.Width, monitorRect.Height),
              LogicalMonitorArea = new Rectangle(monitorRect.Left, monitorRect.Top, monitorRect.Width, monitorRect.Height),
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
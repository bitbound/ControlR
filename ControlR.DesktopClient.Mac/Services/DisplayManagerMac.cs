using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

internal class DisplayManagerMac(ILogger<DisplayManagerMac> logger) : IDisplayManager
{
  private readonly Lock _displayLock = new();
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerMac> _logger = logger;

  public async Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY)
  {
    var findResult = await TryFindDisplay(displayName);
    if (!findResult.IsSuccess)
    {
      return Point.Empty;
    }

    var display = findResult.Value;
    var bounds = display.MonitorArea;
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
    lock (_displayLock)
    {
      try
      {
        EnsureDisplaysLoaded();
        if (_displays.IsEmpty)
        {
          return Rectangle.Empty;
        }

        var minX = _displays.Values.Min(d => d.MonitorArea.Left);
        var minY = _displays.Values.Min(d => d.MonitorArea.Top);
        var maxX = _displays.Values.Max(d => d.MonitorArea.Right);
        var maxY = _displays.Values.Max(d => d.MonitorArea.Bottom);

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting virtual screen bounds.");
        // Return main display bounds as fallback
        var mainDisplayId = CoreGraphics.CGMainDisplayID();
        var bounds = CoreGraphics.CGDisplayBounds(mainDisplayId);
        return new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
      }
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
    if (_displays.IsEmpty)
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
      var displays = DisplayEnumHelperMac.GetDisplays();
      foreach (var display in displays)
      {
        _displays[display.DeviceName] = display;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading display list.");
    }
  }
}
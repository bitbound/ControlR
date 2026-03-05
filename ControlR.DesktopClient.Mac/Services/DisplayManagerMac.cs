using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

internal class DisplayManagerMac(ILogger<DisplayManagerMac> logger, IDisplayEnumHelperMac displayEnumHelper) : IDisplayManager
{
  private readonly IDisplayEnumHelperMac _displayEnumHelper = displayEnumHelper;
  private readonly Lock _displayLock = new();
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerMac> _logger = logger;

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
    lock (_displayLock)
    {
      try
      {
        EnsureDisplaysLoaded();
        if (_displays.IsEmpty)
        {
          return default;
        }

        var minX = _displays.Values.Min(d => d.LogicalMonitorArea.Left);
        var minY = _displays.Values.Min(d => d.LogicalMonitorArea.Top);
        var maxX = _displays.Values.Max(d => d.LogicalMonitorArea.Right);
        var maxY = _displays.Values.Max(d => d.LogicalMonitorArea.Bottom);

        return new LogicalRect(minX, minY, maxX - minX, maxY - minY);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting virtual logical screen bounds.");
        var mainDisplayId = CoreGraphics.CGMainDisplayID();
        var bounds = CoreGraphics.CGDisplayBounds(mainDisplayId);
        return new LogicalRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
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
      var displays = _displayEnumHelper.GetDisplays();
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
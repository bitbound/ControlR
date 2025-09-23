using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Mac.Helpers;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

internal class DisplayManagerMac(ILogger<DisplayManagerMac> logger) : IDisplayManager
{
  private readonly Lock _displayLock = new();
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerMac> _logger = logger;

  public Task<Point> ConvertPercentageLocationToAbsolute(string displayName, double percentX, double percentY)
  {
    if (!TryFindDisplay(displayName, out var display))
    {
      return Task.FromResult(Point.Empty);
    }

    var bounds = display.MonitorArea;
    var absoluteX = (int)(bounds.Left + bounds.Width * percentX);
    var absoluteY = (int)(bounds.Top + bounds.Height * percentY);

    return Task.FromResult(new Point(absoluteX, absoluteY));
  }

  public Task<ImmutableList<DisplayDto>> GetDisplays()
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();

      var displayDtos = _displays
        .Values
        .Select(x => new DisplayDto
        {
          DisplayId = x.DeviceName,
          Height = x.MonitorArea.Height,
          IsPrimary = x.IsPrimary,
          Width = x.MonitorArea.Width,
          Name = x.DisplayName,
          Top = x.MonitorArea.Top,
          Left = x.MonitorArea.Left,
          ScaleFactor = x.ScaleFactor,
        })
        .ToImmutableList();

      return Task.FromResult(displayDtos);
    }
  }

  public DisplayInfo? GetPrimaryDisplay()
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      return _displays.Values.FirstOrDefault(x => x.IsPrimary)
        ?? _displays.Values.FirstOrDefault();
    }
  }

  public Rectangle GetVirtualScreenBounds()
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
      // Return main display bounds as fallback
      var mainDisplayId = CoreGraphics.CGMainDisplayID();
      var bounds = CoreGraphics.CGDisplayBounds(mainDisplayId);
      return new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
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

  public bool TryFindDisplay(string deviceName, [NotNullWhen(true)] out DisplayInfo? display)
  {
    lock (_displayLock)
    {
      EnsureDisplaysLoaded();
      return _displays.TryGetValue(deviceName, out display);
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
      var displays = DisplaysEnumerationHelper.GetDisplays();
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
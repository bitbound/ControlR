using System.Collections.Concurrent;
using System.Collections.Immutable;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Windows.Helpers;
using ControlR.Libraries.NativeInterop.Windows;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.Shared.Primitives;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Drawing;

namespace ControlR.DesktopClient.Windows.Services;

internal interface IDisplayManagerWindows : IDisplayManager
{
}

internal class DisplayManagerWindows(
  IWin32Interop win32Interop,
  IWindowsMessagePump messagePump,
  ILogger<DisplayManagerWindows> logger) : IDisplayManager, IDisplayManagerWindows
{
  private readonly Lock _displayLock = new();
  private readonly ConcurrentDictionary<string, DisplayInfo> _displays = new();
  private readonly ILogger<DisplayManagerWindows> _logger = logger;
  private readonly IWindowsMessagePump _messagePump = messagePump;
  private readonly IWin32Interop _win32Interop = win32Interop;

  private nint _privacyWindow = nint.Zero;

  public bool IsPrivacyScreenEnabled => _privacyWindow != nint.Zero;

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

  public Task<Rectangle> GetVirtualScreenLayoutBounds()
  {
    var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
    var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
    var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
    var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);

    return Task.FromResult(new Rectangle(left, top, width, height));
  }

  public Task ReloadDisplays()
  {
    lock (_displayLock)
    {
      ReloadDisplaysImpl();
    }
    return Task.CompletedTask;
  }

  public async Task<Result> SetPrivacyScreen(bool isEnabled)
  {
    try
    {
      if (isEnabled)
      {
        if (_privacyWindow != nint.Zero)
        {
          _logger.LogWarning("Privacy screen is already enabled");
          return Result.Ok();
        }

        var bounds = await GetVirtualScreenLayoutBounds();
        _privacyWindow = await _messagePump.InvokeOnWindowThread(() =>
          _win32Interop.CreatePrivacyScreenWindow(
            (int)bounds.X,
            (int)bounds.Y,
            (int)bounds.Width,
            (int)bounds.Height));

        if (_privacyWindow == nint.Zero)
        {
          _logger.LogError("Failed to create privacy screen window");
          return Result.Fail("Failed to create privacy screen window");
        }

        _logger.LogInformation("Enabled privacy screen");
        return Result.Ok();
      }
      else
      {
        if (_privacyWindow == nint.Zero)
        {
          _logger.LogWarning("Privacy screen is not enabled");
          return Result.Ok();
        }

        var windowHandle = _privacyWindow;
        _privacyWindow = nint.Zero;

        await _messagePump.InvokeOnWindowThread(() =>
          _win32Interop.DestroyPrivacyScreenWindow(windowHandle));

        _privacyWindow = nint.Zero;
        _logger.LogInformation("Disabled privacy screen");
        return Result.Ok();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to {Action} privacy screen", isEnabled ? "enable" : "disable");
      return Result.Fail($"Failed to {(isEnabled ? "enable" : "disable")} privacy screen");
    }
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
      foreach (var display in DisplayEnumHelperWindows.GetDisplays())
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
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Linux.Services;

/// <summary>
/// Input simulator for Wayland using XDG Desktop Portal RemoteDesktop interface.
///
/// Implementation:
/// - Uses XDG Desktop Portal for permission management and session setup
/// - Calls portal DBus methods for pointer and keyboard simulation
/// - Maps browser key codes to Linux evdev keycodes
///
/// Wayland security model:
/// 1. Create RemoteDesktop session via portal
/// 2. User grants permission via system dialog (one-time)
/// 3. Application can simulate input events via portal DBus calls
/// </summary>
public class InputSimulatorWayland(ILogger<InputSimulatorWayland> logger) : IInputSimulator, IDisposable
{
  private readonly ILogger<InputSimulatorWayland> _logger = logger;
  private XdgDesktopPortal? _portal;
  private string? _sessionHandle;
  private uint _streamNodeId;
  private bool _isInitialized;
  private bool _permissionDenied;
  private bool _disposed;
  private readonly SemaphoreSlim _initLock = new(1, 1);

  public void InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    if (_permissionDenied || !EnsureInitializedAsync().GetAwaiter().GetResult())
    {
      return;
    }

    try
    {
      var keycode = LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(code);
      if (keycode < 0)
      {
        _logger.LogWarning("Unknown key code: {Code}", code);
        return;
      }

      _portal!.NotifyKeyboardKeycodeAsync(_sessionHandle!, keycode, isPressed).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating key event on Wayland: {Code}", code);
    }
  }

  public void InvokeMouseButtonEvent(int x, int y, DisplayInfo? display, int button, bool isPressed)
  {
    if (_permissionDenied || !EnsureInitializedAsync().GetAwaiter().GetResult())
    {
      return;
    }

    try
    {
      var linuxButton = LinuxKeycodeMapper.MouseButtonToLinuxCode(button);
      _portal!.NotifyPointerButtonAsync(_sessionHandle!, linuxButton, isPressed).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating mouse button event on Wayland");
    }
  }

  public void MovePointer(int x, int y, DisplayInfo? display, MovePointerType moveType)
  {
    if (_permissionDenied || !EnsureInitializedAsync().GetAwaiter().GetResult())
    {
      return;
    }

    try
    {
      if (moveType == MovePointerType.Absolute)
      {
        // Use absolute positioning relative to the stream
        _portal!.NotifyPointerMotionAbsoluteAsync(_sessionHandle!, _streamNodeId, x, y).GetAwaiter().GetResult();
      }
      else
      {
        // Relative movement - calculate delta from current position
        // Note: In practice, the caller should provide deltas for relative movement
        _portal!.NotifyPointerMotionAsync(_sessionHandle!, x, y).GetAwaiter().GetResult();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating pointer motion on Wayland");
    }
  }

  public void ResetKeyboardState()
  {
    // Not applicable for Wayland RemoteDesktop portal
    _logger.LogDebug("Keyboard state reset not applicable on Wayland");
  }

  public void ScrollWheel(int x, int y, DisplayInfo? display, int scrollY, int scrollX)
  {
    if (_permissionDenied || !EnsureInitializedAsync().GetAwaiter().GetResult())
    {
      return;
    }

    try
    {
      // Use discrete scroll for more reliable behavior
      if (scrollY != 0)
      {
        var steps = scrollY / 120; // 120 units per notch
        _portal!.NotifyPointerAxisDiscreteAsync(_sessionHandle!, 0, steps).GetAwaiter().GetResult();
      }

      if (scrollX != 0)
      {
        var steps = scrollX / 120;
        _portal!.NotifyPointerAxisDiscreteAsync(_sessionHandle!, 1, steps).GetAwaiter().GetResult();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating scroll wheel on Wayland");
    }
  }

  public Task SetBlockInput(bool isBlocked)
  {
    throw new NotImplementedException("Input blocking is not supported on Wayland");
  }

  public void TypeText(string text)
  {
    if (_permissionDenied || !EnsureInitializedAsync().GetAwaiter().GetResult())
    {
      return;
    }

    try
    {
      foreach (var ch in text)
      {
        var (keycode, needsShift) = CharacterToKeycode(ch);
        if (keycode < 0)
        {
          _logger.LogDebug("No keycode mapping for character: {Char}", ch);
          continue;
        }

        if (needsShift)
        {
          _portal!.NotifyKeyboardKeycodeAsync(_sessionHandle!, 42, true).GetAwaiter().GetResult();
        }

        _portal!.NotifyKeyboardKeycodeAsync(_sessionHandle!, keycode, true).GetAwaiter().GetResult();
        Thread.Sleep(10);
        _portal!.NotifyKeyboardKeycodeAsync(_sessionHandle!, keycode, false).GetAwaiter().GetResult();

        if (needsShift)
        {
          _portal!.NotifyKeyboardKeycodeAsync(_sessionHandle!, 42, false).GetAwaiter().GetResult();
        }

        Thread.Sleep(10);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error typing text on Wayland");
    }
  }

  private static (int Keycode, bool NeedsShift) CharacterToKeycode(char ch)
  {
    return ch switch
    {
      >= 'a' and <= 'z' => (LinuxKeycodeMapper.BrowserCodeToLinuxKeycode($"Key{char.ToUpper(ch)}"), false),
      >= 'A' and <= 'Z' => (LinuxKeycodeMapper.BrowserCodeToLinuxKeycode($"Key{ch}"), true),
      >= '0' and <= '9' => (LinuxKeycodeMapper.BrowserCodeToLinuxKeycode($"Digit{ch}"), false),
      ' ' => (57, false),
      '!' => (2, true),
      '@' => (3, true),
      '#' => (4, true),
      '$' => (5, true),
      '%' => (6, true),
      '^' => (7, true),
      '&' => (8, true),
      '*' => (9, true),
      '(' => (10, true),
      ')' => (11, true),
      '-' => (12, false),
      '_' => (12, true),
      '=' => (13, false),
      '+' => (13, true),
      '[' => (26, false),
      '{' => (26, true),
      ']' => (27, false),
      '}' => (27, true),
      '\\' => (43, false),
      '|' => (43, true),
      ';' => (39, false),
      ':' => (39, true),
      '\'' => (40, false),
      '"' => (40, true),
      ',' => (51, false),
      '<' => (51, true),
      '.' => (52, false),
      '>' => (52, true),
      '/' => (53, false),
      '?' => (53, true),
      '`' => (41, false),
      '~' => (41, true),
      '\n' => (28, false),
      '\t' => (15, false),
      _ => (-1, false)
    };
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _portal?.Dispose();
    _portal = null;

    _initLock?.Dispose();

    _disposed = true;
  }

  private async Task<bool> EnsureInitializedAsync()
  {
    if (_isInitialized)
    {
      return true;
    }

    if (_permissionDenied)
    {
      return false;
    }

    await _initLock.WaitAsync();
    try
    {
      if (_isInitialized)
      {
        return true;
      }

      _portal = await XdgDesktopPortal.CreateAsync(_logger);

      if (!await _portal.IsRemoteDesktopAvailableAsync())
      {
        _logger.LogError(
          "XDG Desktop Portal RemoteDesktop is not available. " +
          "Ensure xdg-desktop-portal and a backend are installed.");
        return false;
      }

      // Create RemoteDesktop session
      var sessionResult = await _portal.CreateRemoteDesktopSessionAsync();
      if (!sessionResult.IsSuccess || sessionResult.Value is null)
      {
        _logger.LogError("Failed to create RemoteDesktop session: {Error}", sessionResult.Reason);
        return false;
      }

      _sessionHandle = sessionResult.Value;
      _logger.LogInformation("Created RemoteDesktop session: {Session}", _sessionHandle);

      // Select devices (keyboard and pointer)
      var selectResult = await _portal.SelectRemoteDesktopDevicesAsync(
        _sessionHandle,
        deviceTypes: 3);  // 1 = keyboard, 2 = pointer, 3 = both

      if (!selectResult.IsSuccess)
      {
        _logger.LogError("Failed to select RemoteDesktop devices: {Error}", selectResult.Reason);
        return false;
      }

      // Start the session (shows permission dialog to user)
      var startResult = await _portal.StartRemoteDesktopAsync(_sessionHandle);
      if (!startResult.IsSuccess)
      {
        _logger.LogError("Failed to start RemoteDesktop: {Error}", startResult.Reason);
        _permissionDenied = true;
        return false;
      }

      // Get stream node ID if available (needed for absolute positioning)
      if (startResult.Value != null && startResult.Value.Count > 0)
      {
        _streamNodeId = startResult.Value[0].NodeId;
      }

      _isInitialized = true;
      _logger.LogInformation("Wayland input simulation fully initialized");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error initializing Wayland input simulation");
      return false;
    }
    finally
    {
      _initLock.Release();
    }
  }
}

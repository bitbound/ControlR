using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Helpers;
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
public class InputSimulatorWayland(
  IWaylandPortalAccessor portalAccessor,
  ILogger<InputSimulatorWayland> logger) : IInputSimulator, IDisposable
{
  private readonly ILogger<InputSimulatorWayland> _logger = logger;
  private readonly IWaylandPortalAccessor _portalAccessor = portalAccessor;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private XdgDesktopPortal? _portal;
  private string? _sessionHandle;
  private uint _streamNodeId;
  private bool _isInitialized;
  private bool _disposed;

  public async Task InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      var isPrintableCharacter = string.IsNullOrWhiteSpace(code) && key.Length == 1;

      if (isPrintableCharacter)
      {
        if (isPressed)
        {
          await TypeText(key);
        }
        return;
      }
      
      var keycode = LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(code);
      if (keycode < 0)
      {
        _logger.LogWarning("Unknown key code: {Code}", code);
        return;
      }

      Guard.IsNotNull(_portal);
      Guard.IsNotNull(_sessionHandle);

      await _portal.NotifyKeyboardKeycodeAsync(_sessionHandle, keycode, isPressed);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating key event on Wayland: {Code}", code);
    }
  }

  public async Task InvokeMouseButtonEvent(PointerCoordinates coordinates, int button, bool isPressed)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      var linuxButton = LinuxKeycodeMapper.MouseButtonToLinuxCode(button);

      Guard.IsNotNull(_portal);
      Guard.IsNotNull(_sessionHandle);

      await _portal.NotifyPointerButtonAsync(_sessionHandle, linuxButton, isPressed);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating mouse button event on Wayland");
    }
  }

  public async Task MovePointer(PointerCoordinates coordinates, MovePointerType moveType)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      Guard.IsNotNull(_portal);
      Guard.IsNotNull(_sessionHandle);

      switch (moveType)
      {
        case MovePointerType.Absolute:
          {
            await _portal.NotifyPointerMotionAbsoluteAsync(
              _sessionHandle,
              _streamNodeId,
              coordinates.AbsolutePoint.X,
              coordinates.AbsolutePoint.Y);
            break;
          }

        case MovePointerType.Relative:
          await _portal.NotifyPointerMotionAsync(_sessionHandle, coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y);
          break;
        default:
          _logger.LogWarning("Unknown move type: {MoveType}", moveType);
          break;
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating pointer motion on Wayland");
    }
  }

  public Task ResetKeyboardState()
  {
    // Not applicable for Wayland RemoteDesktop portal
    _logger.LogDebug("Keyboard state reset not applicable on Wayland");
    return Task.CompletedTask;
  }

  public async Task ScrollWheel(PointerCoordinates coordinates, int scrollY, int scrollX)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      Guard.IsNotNull(_portal);
      Guard.IsNotNull(_sessionHandle);

      const int scrollSteps = 3;

      // Use discrete scroll for more reliable behavior
      if (scrollY != 0)
      {
        var steps = scrollY < 0 ? scrollSteps : -scrollSteps;
        await _portal.NotifyPointerAxisDiscreteAsync(_sessionHandle, 0, steps);
      }

      if (scrollX != 0)
      {
        var steps = scrollX < 0 ? scrollSteps : -scrollSteps;
        await _portal.NotifyPointerAxisDiscreteAsync(_sessionHandle, 1, steps);
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

  public async Task TypeText(string text)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      Guard.IsNotNull(_portal);
      Guard.IsNotNull(_sessionHandle);

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
          await _portal.NotifyKeyboardKeycodeAsync(_sessionHandle, 42, true);
        }

        await _portal.NotifyKeyboardKeycodeAsync(_sessionHandle, keycode, true);
        await Task.Delay(10);
        await _portal.NotifyKeyboardKeycodeAsync(_sessionHandle, keycode, false);

        if (needsShift)
        {
          await _portal.NotifyKeyboardKeycodeAsync(_sessionHandle, 42, false);
        }

        await Task.Delay(10);
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

    _initLock?.Dispose();
    _disposed = true;
  }

  private async Task<bool> EnsureInitializedAsync()
  {
    if (_isInitialized && _portal is not null && _sessionHandle is not null)
    {
      return true;
    }

    await _initLock.WaitAsync();
    try
    {
      if (_isInitialized && _portal is not null && _sessionHandle is not null)
      {
        return true;
      }

      var remoteDesktopSession = await _portalAccessor.GetRemoteDesktopSession();
      if (remoteDesktopSession == null)
      {
        _logger.LogError("Failed to get RemoteDesktop session from portal accessor");
        return false;
      }

      _portal = remoteDesktopSession.Value.Portal;
      _sessionHandle = remoteDesktopSession.Value.SessionHandle;

      var screenCastStreams = await _portalAccessor.GetScreenCastStreams();
      if (screenCastStreams.Count > 0)
      {
        _streamNodeId = screenCastStreams[0].NodeId;
      }
      else
      {
        _logger.LogWarning("No ScreenCast streams available. Absolute positioning may not work.");
      }

      _isInitialized = true;
      _logger.LogInformation("Wayland input simulation initialized");
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

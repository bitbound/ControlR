using System.Collections.Concurrent;
using System.Collections.Generic;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.NativeInterop.Linux;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;
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
  IXdgDesktopPortal desktopPortal,
  IDesktopCapturerFactory desktopCapturerFactory,
  ILogger<InputSimulatorWayland> logger) : IInputSimulator, IDisposable
{
  private static bool _keysymResolutionUnavailable;

  private readonly IDesktopCapturer _desktopCapturer = desktopCapturerFactory.GetOrCreate();
  private readonly IXdgDesktopPortal _desktopPortal = desktopPortal;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private readonly SemaphoreSlim _keyboardLock = new(1, 1);
  private readonly ILogger<InputSimulatorWayland> _logger = logger;
  private readonly HashSet<int> _pressedKeycodes = [];
  private readonly HashSet<int> _pressedKeysyms = [];
  private readonly ConcurrentDictionary<int, PipeWireStreamInfo> _screenCastStreams = new();

  private bool _disposed;
  private bool _isInitialized;
  private string? _sessionHandle;

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _keyboardLock.Dispose();
    _initLock.Dispose();
    _disposed = true;
    GC.SuppressFinalize(this);
  }

  public async Task InvokeKeyEvent(
    string key,
    string code,
    bool isPressed,
    KeyboardInputMode inputMode,
    KeyEventModifiersDto modifiers)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      await _keyboardLock.WaitAsync();
      try
      {
        Guard.IsNotNull(_sessionHandle);

        if (ShouldUseLogicalKeysymInput(key, inputMode, modifiers) &&
            TryGetKeysym(key, out var keysym))
        {
          await SendKeysymAsync(_sessionHandle, keysym, isPressed);
          return;
        }

        if (inputMode == KeyboardInputMode.Virtual && (!HasShortcutModifier(modifiers) || IsModifierKey(key)))
        {
          code = string.Empty;
        }

        var keycode = LinuxKeycodeMapper.BrowserCodeToLinuxKeycode(code, key);
        if (keycode < 0)
        {
          _logger.LogWarning("Unknown key: Code={Code}, Key={Key}", code, key);
          return;
        }

        await SendKeycodeAsync(_sessionHandle, keycode, isPressed);
      }
      finally
      {
        _keyboardLock.Release();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating key event on Wayland: Code={Code}, Key={Key}", code, key);
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

      Guard.IsNotNull(_sessionHandle);

      await _desktopPortal.NotifyPointerButtonAsync(_sessionHandle, linuxButton, isPressed);
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
      Guard.IsNotNull(_sessionHandle);

      switch (moveType)
      {
        case MovePointerType.Absolute:
          {
            var selectedDisplayResult = await _desktopCapturer.TryGetSelectedDisplay();
            if (!selectedDisplayResult.IsSuccess)
            {
              _logger.LogWarning("Cannot move pointer absolutely: no display selected");
              return;
            }

            if (!int.TryParse(selectedDisplayResult.Value.DeviceName, out var deviceIndex))
            {
              _logger.LogWarning("Cannot move pointer absolutely: invalid display device name {DeviceName}", selectedDisplayResult.Value.DeviceName);
              return;
            }
            if (!_screenCastStreams.TryGetValue(deviceIndex, out var streamInfo))
            {
              _logger.LogWarning("Cannot move pointer absolutely: no stream info for display index {DeviceIndex}", deviceIndex);
              return;
            }

            // Wayland NotifyPointerMotionAbsolute takes coordinates in the stream's physical
            // pixel space (0 to stream width/height), not global virtual screen coordinates.
            var clampedX = Math.Clamp(coordinates.NormalizedX, 0, 1);
            var clampedY = Math.Clamp(coordinates.NormalizedY, 0, 1);
            var maxX = Math.Max(0, coordinates.Display.LayoutBounds.Width - 1);
            var maxY = Math.Max(0, coordinates.Display.LayoutBounds .Height - 1);
            var logicalX = maxX * clampedX;
            var logicalY = maxY * clampedY;

            await _desktopPortal.NotifyPointerMotionAbsoluteAsync(
              _sessionHandle,
              streamInfo.NodeId,
              logicalX,
              logicalY);
            break;
          }

        case MovePointerType.Relative:
          _logger.LogWarning("Relative pointer movement is not supported by the remote control input path on Wayland.");
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

  public async Task ResetKeyboardState()
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    Guard.IsNotNull(_sessionHandle);

    await _keyboardLock.WaitAsync();
    try
    {
      var keycodes = _pressedKeycodes.ToArray();
      var keysyms = _pressedKeysyms.ToArray();

      foreach (var keycode in keycodes)
      {
        await SendKeycodeAsync(_sessionHandle, keycode, false);
      }

      foreach (var keysym in keysyms)
      {
        await SendKeysymAsync(_sessionHandle, keysym, false);
      }

      var releasedCount = keycodes.Length + keysyms.Length;
      if (releasedCount > 0)
      {
        _logger.LogDebug("Released {Count} tracked Wayland keys during keyboard state reset", releasedCount);
      }
      else
      {
        _logger.LogDebug("No tracked Wayland keys found during keyboard state reset");
      }
    }
    finally
    {
      _keyboardLock.Release();
    }
  }

  public async Task ScrollWheel(PointerCoordinates coordinates, int scrollY, int scrollX)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      Guard.IsNotNull(_sessionHandle);

      const int scrollSteps = 3;

      // Use discrete scroll for more reliable behavior
      if (scrollY != 0)
      {
        var steps = scrollY < 0 ? scrollSteps : -scrollSteps;
        await _desktopPortal.NotifyPointerAxisDiscreteAsync(_sessionHandle, 0, steps);
      }

      if (scrollX != 0)
      {
        var steps = scrollX < 0 ? scrollSteps : -scrollSteps;
        await _desktopPortal.NotifyPointerAxisDiscreteAsync(_sessionHandle, 1, steps);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error simulating scroll wheel on Wayland");
    }
  }

  public Task<bool> SetBlockInput(bool isBlocked)
  {
    return false.AsTaskResult();
  }

  public async Task TypeText(string text)
  {
    if (!await EnsureInitializedAsync())
    {
      return;
    }

    try
    {
      Guard.IsNotNull(_sessionHandle);

      await _keyboardLock.WaitAsync();
      try
      {
      foreach (var ch in text)
      {
        if (TryGetKeysym(ch.ToString(), out var keysym))
        {
          await SendKeysymAsync(_sessionHandle, keysym, true);
          await Task.Delay(1);
          await SendKeysymAsync(_sessionHandle, keysym, false);
          continue;
        }

        if (!LinuxKeycodeMapper.TryMapCharacterToLinuxKeycode(ch, out var keycode, out var needsShift))
        {
          _logger.LogDebug("No keycode mapping for character: {Char}", ch);
          continue;
        }

        if (needsShift)
        {
          await SendKeycodeAsync(_sessionHandle, 42, true);
        }

        await SendKeycodeAsync(_sessionHandle, keycode, true);
        await Task.Delay(1);
        await SendKeycodeAsync(_sessionHandle, keycode, false);

        if (needsShift)
        {
          await SendKeycodeAsync(_sessionHandle, 42, false);
        }

        await Task.Delay(1);
      }
      }
      finally
      {
        _keyboardLock.Release();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error typing text on Wayland");
    }
  }

  private static bool HasShortcutModifier(KeyEventModifiersDto modifiers)
  {
    return modifiers.Control || modifiers.Alt || modifiers.Meta;
  }

  private static bool IsModifierKey(string key)
  {
    return key is "Shift" or "Control" or "Alt" or "Meta";
  }

  private static bool IsPrintableTextKey(string key)
  {
    return key.Length == 1 && !char.IsControl(key[0]);
  }

  private static bool ShouldUseLogicalKeysymInput(string key, KeyboardInputMode inputMode, KeyEventModifiersDto modifiers)
  {
    if (inputMode == KeyboardInputMode.Physical)
    {
      return false;
    }

    if (HasShortcutModifier(modifiers))
    {
      return false;
    }

    // In Auto mode, restrict keysym usage to printable text keys so that
    // commands/navigation/non-text keys continue to use physical-style handling.
    if (inputMode == KeyboardInputMode.Auto)
    {
      return IsPrintableTextKey(key);
    }

    // In Virtual mode (and any other non-physical mode), keep the existing
    // behavior of using keysym for non-modifier keys without shortcut modifiers.
    if (inputMode == KeyboardInputMode.Virtual)
    {
      return !IsModifierKey(key);
    }

    return false;
  }

  private static bool TryGetKeysym(string? key, out int keysym)
  {
    // Cache libxkbcommon unavailability to avoid repeated exception costs.

    if (_keysymResolutionUnavailable)
    {
      keysym = 0;
      return false;
    }

    keysym = 0;
    var name = LinuxKeycodeMapper.BrowserKeyToKeysymName(key);
    if (string.IsNullOrWhiteSpace(name))
    {
      return false;
    }

    uint resolvedKeysym;
    try
    {
      resolvedKeysym = LibXkbCommon.xkb_keysym_from_name(name, 0);
    }
    catch (DllNotFoundException)
    {
      _keysymResolutionUnavailable = true;
      return false;
    }
    catch (EntryPointNotFoundException)
    {
      _keysymResolutionUnavailable = true;
      return false;
    }

    if (resolvedKeysym == 0)
    {
      return false;
    }

    keysym = unchecked((int)resolvedKeysym);
    return true;
  }

  private async Task<bool> EnsureInitializedAsync()
  {
    await _initLock.WaitAsync();
    try
    {
      if (!_isInitialized)
      {
        await _desktopPortal.Initialize();
      }

      var sessionHandle = await _desktopPortal.GetRemoteDesktopSessionHandle();

      if (sessionHandle == null)
      {
        _logger.LogError("Failed to get RemoteDesktop session from portal accessor");
        _isInitialized = false;
        _sessionHandle = null;
        _screenCastStreams.Clear();
        return false;
      }

      if (_isInitialized && string.Equals(_sessionHandle, sessionHandle, StringComparison.Ordinal))
      {
        return true;
      }

      var previousSessionHandle = _sessionHandle;
      _sessionHandle = sessionHandle;
      _screenCastStreams.Clear();
      _pressedKeycodes.Clear();
      _pressedKeysyms.Clear();

      var screenCastStreams = await _desktopPortal.GetScreenCastStreams();
      foreach (var stream in screenCastStreams)
      {
        _screenCastStreams[stream.StreamIndex] = stream;
      }

      if (_screenCastStreams.IsEmpty)
      {
        _logger.LogError("No ScreenCast streams available. Absolute positioning will not work.");
      }

      _isInitialized = true;
      if (previousSessionHandle is null)
      {
        _logger.LogInformation("Wayland input simulation initialized");
      }
      else
      {
        _logger.LogInformation(
          "Wayland input simulation synchronized to refreshed portal session {SessionHandle}",
          _sessionHandle);
      }

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

  private async Task SendKeycodeAsync(string sessionHandle, int keycode, bool isPressed)
  {
    await _desktopPortal.NotifyKeyboardKeycodeAsync(sessionHandle, keycode, isPressed);
    if (isPressed)
    {
      _pressedKeycodes.Add(keycode);
    }
    else
    {
      _pressedKeycodes.Remove(keycode);
    }
  }

  private async Task SendKeysymAsync(string sessionHandle, int keysym, bool isPressed)
  {
    await _desktopPortal.NotifyKeyboardKeysymAsync(sessionHandle, keysym, isPressed);
    if (isPressed)
    {
      _pressedKeysyms.Add(keysym);
    }
    else
    {
      _pressedKeysyms.Remove(keysym);
    }
  }
}

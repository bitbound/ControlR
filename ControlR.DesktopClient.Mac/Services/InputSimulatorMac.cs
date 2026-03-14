using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Extensions;

namespace ControlR.DesktopClient.Mac.Services;

public class InputSimulatorMac(
  IMacInterop macInterop,
  ILogger<InputSimulatorMac> logger) : IInputSimulator
{
  private readonly ILogger<InputSimulatorMac> _logger = logger;
  private readonly IMacInterop _macInterop = macInterop;

  public Task InvokeKeyEvent(
    string key,
    string code,
    bool isPressed,
    KeyboardInputMode inputMode,
    KeyEventModifiersDto modifiers)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return Task.CompletedTask;
    }

    var mode = inputMode;
    var isModifierKey = key is "Shift" or "Control" or "Alt" or "Meta" or "Command" or "Option";

    if (mode == KeyboardInputMode.Virtual)
    {
      if (HasShortcutModifier(modifiers) && !isModifierKey)
      {
        return InvokeMacKeyEvent(key, code, isPressed, mode);
      }

      return InvokeMacKeyEvent(key, string.Empty, isPressed, mode);
    }

    return InvokeMacKeyEvent(key, code, isPressed, mode);
  }

  public Task InvokeMouseButtonEvent(PointerCoordinates coordinates, int button, bool isPressed)
  {
    try
    {
      var (logicalX, logicalY) = GetAbsoluteLogicalCoords(coordinates);
      _macInterop.InvokeMouseButtonEvent(logicalX, logicalY, button, isPressed);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing mouse button event for button: {Button}", button);
    }

    return Task.CompletedTask;
  }

  public Task MovePointer(PointerCoordinates coordinates, MovePointerType moveType)
  {
    try
    {
      var (logicalX, logicalY) = GetAbsoluteLogicalCoords(coordinates);
      _macInterop.MovePointer(logicalX, logicalY, moveType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing pointer move for normalized ({X:F4}, {Y:F4})", coordinates.NormalizedX, coordinates.NormalizedY);
    }

    return Task.CompletedTask;
  }

  public Task ResetKeyboardState()
  {
    try
    {
      _macInterop.ResetKeyboardState();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing keyboard state reset");
    }

    return Task.CompletedTask;
  }

  public Task ScrollWheel(PointerCoordinates coordinates, int scrollY, int scrollX)
  {
    try
    {
      var (logicalX, logicalY) = GetAbsoluteLogicalCoords(coordinates);
      _macInterop.InvokeWheelScroll(logicalX, logicalY, scrollY, scrollX);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing wheel scroll for normalized ({X:F4}, {Y:F4})", coordinates.NormalizedX, coordinates.NormalizedY);
    }

    return Task.CompletedTask;
  }

  public Task<bool> SetBlockInput(bool isBlocked)
  {
    return false.AsTaskResult();
  }

  public Task TypeText(string text)
  {
    try
    {
      _macInterop.TypeText(text);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing text input: {Text}", text);
    }

    return Task.CompletedTask;
  }

  /// <summary>
  /// Converts normalized (0-1) display-relative coordinates to absolute logical screen coordinates
  /// using the display's logical monitor area.  macOS input APIs (CGWarpMouseCursorPosition etc.)
  /// expect logical/point coordinates in the global screen coordinate space.
  /// </summary>
  private static (int x, int y) GetAbsoluteLogicalCoords(PointerCoordinates coordinates)
  {
    var bounds = coordinates.Display.LayoutBounds;
    var clampedX = Math.Clamp(coordinates.NormalizedX, 0, 1);
    var clampedY = Math.Clamp(coordinates.NormalizedY, 0, 1);
    var maxX = bounds.Left + Math.Max(0, bounds.Width - 1);
    var maxY = bounds.Top + Math.Max(0, bounds.Height - 1);
    var logicalX = (int)Math.Round(bounds.Left + (Math.Max(0, bounds.Width - 1) * clampedX));
    var logicalY = (int)Math.Round(bounds.Top + (Math.Max(0, bounds.Height - 1) * clampedY));

    logicalX = Math.Clamp(logicalX, bounds.Left, maxX);
    logicalY = Math.Clamp(logicalY, bounds.Top, maxY);
    return (logicalX, logicalY);
  }

  private static bool HasShortcutModifier(KeyEventModifiersDto modifiers)
  {
    return modifiers.Control || modifiers.Alt || modifiers.Meta;
  }

  private Task InvokeMacKeyEvent(string key, string code, bool isPressed, KeyboardInputMode inputMode)
  {
    try
    {
      var result = _macInterop.InvokeKeyEvent(key, code, isPressed, inputMode);
      if (!result.IsSuccess)
      {
        _logger.LogWarning(
          "Failed to invoke key event. Key: {Key}, Code: {Code}, IsPressed: {IsPressed}, InputMode: {InputMode}, Reason: {Reason}",
          key,
          code,
          isPressed,
          inputMode,
          result.Reason);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing key event for key: {Key} (code: {Code})", key, code);
    }

    return Task.CompletedTask;
  }
}

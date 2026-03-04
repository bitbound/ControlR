using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
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
    var isPrintableCharacter = key.Length == 1;
    var isModifierPressed = modifiers.AreAnyPressed;
    var isModifierKey = key is "Shift" or "Control" or "Alt" or "Meta" or "Command" or "Option";

    if (mode == KeyboardInputMode.Virtual)
    {
      if (isPrintableCharacter && !isModifierPressed && !isModifierKey)
      {
        if (isPressed)
        {
          return TypeText(key);
        }

        return Task.CompletedTask;
      }

      if (isModifierPressed && !isModifierKey)
      {
        return InvokeMacKeyEvent(key, code, isPressed, mode);
      }

      return InvokeMacKeyEvent(key, string.Empty, isPressed, mode);
    }

    if (mode == KeyboardInputMode.Auto && isPrintableCharacter && !isModifierPressed)
    {
      if (isPressed)
      {
        return TypeText(key);
      }

      return Task.CompletedTask;
    }

    return InvokeMacKeyEvent(key, code, isPressed, mode);
  }

  public Task InvokeMouseButtonEvent(PointerCoordinates coordinates, int button, bool isPressed)
  {
    try
    {
      // Mac expects the logical coordinates for mouse events, not the pixel coordinates,
      // while Windows expects pixel coordinates.  We'll adjust for Mac.
      var logicalX = coordinates.AbsolutePoint.X / coordinates.Display.ScaleFactor;
      var logicalY = coordinates.AbsolutePoint.Y / coordinates.Display.ScaleFactor;
      _macInterop.InvokeMouseButtonEvent((int)logicalX, (int)logicalY, button, isPressed);
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
      // Mac expects the logical coordinates for pointer moves, not the pixel coordinates,
      // while Windows expects pixel coordinates.  We'll adjust for Mac.
      var logicalX = coordinates.AbsolutePoint.X / coordinates.Display.ScaleFactor;
      var logicalY = coordinates.AbsolutePoint.Y / coordinates.Display.ScaleFactor;
      _macInterop.MovePointer((int)logicalX, (int)logicalY, moveType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing pointer move to ({X}, {Y})", coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y);
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
      // Mac expects the logical coordinates for wheel scrolls, not the pixel coordinates,
      // while Windows expects pixel coordinates.  We'll adjust for Mac.
      var logicalX = coordinates.AbsolutePoint.X / coordinates.Display.ScaleFactor;
      var logicalY = coordinates.AbsolutePoint.Y / coordinates.Display.ScaleFactor;
      _macInterop.InvokeWheelScroll((int)logicalX, (int)logicalY, scrollY, scrollX);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing wheel scroll at ({X}, {Y})", coordinates.AbsolutePoint.X, coordinates.AbsolutePoint.Y);
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

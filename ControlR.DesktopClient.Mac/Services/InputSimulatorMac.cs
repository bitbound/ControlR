using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Mac.Services;

public class InputSimulatorMac(
  IMacInterop macInterop,
  ILogger<InputSimulatorMac> logger) : IInputSimulator
{
  private readonly ILogger<InputSimulatorMac> _logger = logger;
  private readonly IMacInterop _macInterop = macInterop;
  
  public Task InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return Task.CompletedTask;
    }

    // Hybrid approach: route printable characters to Unicode injection, commands to virtual key simulation
    // When code is null/empty, it indicates a printable character that should be typed (not simulated as key)
    var isPrintableCharacter = string.IsNullOrWhiteSpace(code) && key.Length == 1;

    if (isPrintableCharacter)
    {
      // For printable characters, use Unicode injection on key down only
      // Key up events are ignored since TypeText handles both down and up internally
      if (isPressed)
      {
        return TypeText(key);
      }
      return Task.CompletedTask;
    }

    // For commands, shortcuts, and non-printable keys, use virtual key simulation
    try
    {
      var result = _macInterop.InvokeKeyEvent(key, code, isPressed);
      if (!result.IsSuccess)
      {
        _logger.LogWarning("Failed to invoke key event. Key: {Key}, Code: {Code}, IsPressed: {IsPressed}, Reason: {Reason}",
          key, code, isPressed, result.Reason);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing key event for key: {Key} (code: {Code})", key, code);
    }

    return Task.CompletedTask;
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

  public Task SetBlockInput(bool isBlocked)
  {
    throw new NotImplementedException();
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
}

using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Logging;
using ControlR.Libraries.NativeInterop.Unix.MacOs;

namespace ControlR.DesktopClient.Mac.Services;

public class InputSimulatorMac(
  IMacInterop macInterop,
  ILogger<InputSimulatorMac> logger) : IInputSimulator
{
  private readonly ILogger<InputSimulatorMac> _logger = logger;
  private readonly IMacInterop _macInterop = macInterop;
  
  public void InvokeKeyEvent(string key, string code, bool isPressed)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return;
    }

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
  }

  public void InvokeMouseButtonEvent(int x, int y, DisplayInfo? display, int button, bool isPressed)
  {
    try
    {
      if (display is null)
      {
        _logger.LogError("DisplayInfo cannot be null for mouse button events on Mac.");
        return;
      }
      // Mac expects the logical coordinates for mouse events, not the pixel coordinates,
      // while Windows expects pixel coordinates.  We'll adjust for Mac.
      var logicalX = x / display.ScaleFactor;
      var logicalY = y / display.ScaleFactor;
      _macInterop.InvokeMouseButtonEvent((int)logicalX, (int)logicalY, button, isPressed);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing mouse button event for button: {Button}", button);
    }
  }

  public void MovePointer(int x, int y, DisplayInfo? display, MovePointerType moveType)
  {
    try
    {
      if (display is null)
      {
        _logger.LogError("DisplayInfo cannot be null for pointer move events on Mac.");
        return;
      }
      // Mac expects the logical coordinates for pointer moves, not the pixel coordinates,
      // while Windows expects pixel coordinates.  We'll adjust for Mac.
      var logicalX = x / display.ScaleFactor;
      var logicalY = y / display.ScaleFactor;
      _macInterop.MovePointer((int)logicalX, (int)logicalY, moveType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing pointer move to ({X}, {Y})", x, y);
    }
  }

  public void ResetKeyboardState()
  {
    try
    {
      _macInterop.ResetKeyboardState();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing keyboard state reset");
    }
  }

  public void ScrollWheel(int x, int y, DisplayInfo? display, int scrollY, int scrollX)
  {
    try
    {
      if (display is null)
      {
        _logger.LogError("DisplayInfo cannot be null for wheel scroll events on Mac.");
        return;
      }
      // Mac expects the logical coordinates for wheel scrolls, not the pixel coordinates,
      // while Windows expects pixel coordinates.  We'll adjust for Mac.
      var logicalX = x / display.ScaleFactor;
      var logicalY = y / display.ScaleFactor;
      _macInterop.InvokeWheelScroll((int)logicalX, (int)logicalY, scrollY, scrollX);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing wheel scroll at ({X}, {Y})", x, y);
    }
  }

  public Task SetBlockInput(bool isBlocked)
  {
    throw new NotImplementedException();
  }

  public void TypeText(string text)
  {
    try
    {
      _macInterop.TypeText(text);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing text input: {Text}", text);
    }
  }
}

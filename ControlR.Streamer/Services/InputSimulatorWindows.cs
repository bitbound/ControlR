using ControlR.Libraries.Shared.Dtos.SidecarDtos;

namespace ControlR.Streamer.Services;

public interface IInputSimulator
{
  void InvokeKeyEvent(string key, bool isPressed);
  void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed);
  void MovePointer(int x, int y, MovePointerType moveType);
  void ResetKeyboardState();
  void ScrollWheel(int x, int y, int scrollY, int scrollX);
  void TypeText(string text);
}

internal class InputSimulatorWindows(
  IWin32Interop win32Interop,
  ILogger<InputSimulatorWindows> logger) : IInputSimulator
{
  public void InvokeKeyEvent(string key, bool isPressed)
  {
    if (string.IsNullOrEmpty(key))
    {
      logger.LogWarning("Key cannot be empty.");
      return;
    }

    TryOnInputDesktop(() =>
    {
      var result = win32Interop.InvokeKeyEvent(key, isPressed);
      if (!result.IsSuccess)
      {
        logger.LogWarning("Failed to invoke key event. Key: {Key}, IsPressed: {IsPressed}, Reason: {Reason}", key,
          isPressed, result.Reason);
      }
    });
  }

  public void InvokeMouseButtonEvent(int x, int y, int button, bool isPressed)
  {
    TryOnInputDesktop(() => { win32Interop.InvokeMouseButtonEvent(x, y, button, isPressed); });
  }

  public void MovePointer(int x, int y, MovePointerType moveType)
  {
    TryOnInputDesktop(() => { win32Interop.MovePointer(x, y, moveType); });
  }

  public void ResetKeyboardState()
  {
    TryOnInputDesktop(() => { win32Interop.ResetKeyboardState(); });
  }

  public void ScrollWheel(int x, int y, int scrollY, int scrollX)
  {
    TryOnInputDesktop(() => { win32Interop.InvokeWheelScroll(x, y, scrollY, scrollX); });
  }

  public void TypeText(string text)
  {
    TryOnInputDesktop(() => { win32Interop.TypeText(text); });
  }

  private void TryOnInputDesktop(Action action)
  {
    try
    {
      win32Interop.SwitchToInputDesktop();
      action.Invoke();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error processing input simulator action.");
    }
  }
}
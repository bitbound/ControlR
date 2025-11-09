using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;
public interface IInputSimulator
{
  void InvokeKeyEvent(string key, string? code, bool isPressed);
  void InvokeMouseButtonEvent(int x, int y, DisplayInfo? display,  int button, bool isPressed);
  void MovePointer(int x, int y, DisplayInfo? display, MovePointerType moveType);
  void ResetKeyboardState();
  void ScrollWheel(int x, int y, DisplayInfo? display, int scrollY, int scrollX);
  Task SetBlockInput(bool isBlocked);
  void TypeText(string text);
}

using ControlR.DesktopClient.Common.Models;
using ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;
public interface IInputSimulator
{
  Task InvokeKeyEvent(string key, string? code, bool isPressed);
  Task InvokeMouseButtonEvent(PointerCoordinates coordinates, int button, bool isPressed);
  Task MovePointer(PointerCoordinates coordinates, MovePointerType moveType);
  Task ResetKeyboardState();
  Task ScrollWheel(PointerCoordinates coordinates, int scrollY, int scrollX);
  Task SetBlockInput(bool isBlocked);
  Task TypeText(string text);
}

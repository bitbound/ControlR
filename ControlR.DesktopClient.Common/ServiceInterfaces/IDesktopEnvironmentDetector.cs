namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IDesktopEnvironmentDetector
{
  DesktopEnvironmentType GetDesktopEnvironment();
  bool IsWayland();
  bool IsX11();
}

public enum DesktopEnvironmentType
{
  Unknown,
  X11,
  Wayland
}

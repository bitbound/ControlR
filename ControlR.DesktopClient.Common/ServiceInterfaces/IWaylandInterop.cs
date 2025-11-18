namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IWaylandInterop
{
  bool IsScreenCastPermissionGranted();
  bool IsRemoteDesktopPermissionGranted();
  void OpenWaylandPermissionsInfo();
}

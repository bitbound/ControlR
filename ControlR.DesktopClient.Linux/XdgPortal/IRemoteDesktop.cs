using Tmds.DBus;

namespace ControlR.DesktopClient.Linux.XdgPortal;

[DBusInterface("org.freedesktop.portal.RemoteDesktop")]
public interface IRemoteDesktop : IDBusObject
{
  Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
  Task<T> GetAsync<T>(string prop);
  Task NotifyKeyboardKeycodeAsync(ObjectPath sessionHandle, IDictionary<string, object> options, int keycode, uint state);
  Task NotifyPointerAxisAsync(ObjectPath sessionHandle, IDictionary<string, object> options, double dx, double dy);
  Task NotifyPointerAxisDiscreteAsync(ObjectPath sessionHandle, IDictionary<string, object> options, uint axis, int steps);
  Task NotifyPointerButtonAsync(ObjectPath sessionHandle, IDictionary<string, object> options, int button, uint state);
  Task NotifyPointerMotionAbsoluteAsync(ObjectPath sessionHandle, IDictionary<string, object> options, uint stream, double x, double y);
  Task NotifyPointerMotionAsync(ObjectPath sessionHandle, IDictionary<string, object> options, double dx, double dy);
  Task<ObjectPath> SelectDevicesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
  Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);
}

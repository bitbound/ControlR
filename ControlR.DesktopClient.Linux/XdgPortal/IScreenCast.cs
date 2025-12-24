using Microsoft.Win32.SafeHandles;
using Tmds.DBus;

namespace ControlR.DesktopClient.Linux.XdgPortal;

[DBusInterface("org.freedesktop.portal.ScreenCast")]
public interface IScreenCast : IDBusObject
{
  Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
  Task<T> GetAsync<T>(string prop);
  Task<SafeFileHandle> OpenPipeWireRemoteAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
  Task<ObjectPath> SelectSourcesAsync(ObjectPath sessionHandle, IDictionary<string, object> options);
  Task<ObjectPath> StartAsync(ObjectPath sessionHandle, string parentWindow, IDictionary<string, object> options);
}

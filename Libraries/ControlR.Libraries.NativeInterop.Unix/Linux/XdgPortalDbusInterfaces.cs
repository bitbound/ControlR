using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;
using Microsoft.Win32.SafeHandles;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

[DBusInterface("org.freedesktop.DBus.Properties")]
internal interface IOrgFreedesktopDBusProperties : IDBusObject
{
  // Per org.freedesktop.DBus.Properties spec: Get(string interface, string property) -> variant
  Task<object> GetAsync(string iface, string prop);
  Task<IDictionary<string, object>> GetAllAsync(string iface);
}

[DBusInterface("org.freedesktop.portal.Request")]
internal interface IPortalRequest : IDBusObject
{
  Task<IDisposable> WatchResponseAsync(Action<uint, IDictionary<string, object>> handler);
}

[DBusInterface("org.freedesktop.portal.ScreenCast")]
internal interface IPortalScreenCast : IDBusObject
{
  Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
  Task<ObjectPath> SelectSourcesAsync(ObjectPath session, IDictionary<string, object> options);
  Task<ObjectPath> StartAsync(ObjectPath session, string parentWindow, IDictionary<string, object> options);
  Task<SafeFileHandle> OpenPipeWireRemoteAsync(ObjectPath session, IDictionary<string, object> options);
}

[DBusInterface("org.freedesktop.portal.RemoteDesktop")]
internal interface IPortalRemoteDesktop : IDBusObject
{
  Task<ObjectPath> CreateSessionAsync(IDictionary<string, object> options);
  Task<ObjectPath> SelectDevicesAsync(ObjectPath session, IDictionary<string, object> options);
  Task<ObjectPath> StartAsync(ObjectPath session, string parentWindow, IDictionary<string, object> options);

  Task NotifyKeyboardKeycodeAsync(ObjectPath session, IDictionary<string, object> options, int keycode, uint state);
  Task NotifyPointerAxisAsync(ObjectPath session, IDictionary<string, object> options, double dx, double dy);
  Task NotifyPointerAxisDiscreteAsync(ObjectPath session, IDictionary<string, object> options, uint axis, int steps);
  Task NotifyPointerButtonAsync(ObjectPath session, IDictionary<string, object> options, int button, uint state);
  Task NotifyPointerMotionAsync(ObjectPath session, IDictionary<string, object> options, double dx, double dy);
  Task NotifyPointerMotionAbsoluteAsync(ObjectPath session, IDictionary<string, object> options, uint stream, double x, double y);
}

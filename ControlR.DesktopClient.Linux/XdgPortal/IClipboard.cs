using Microsoft.Win32.SafeHandles;
using Tmds.DBus;

namespace ControlR.DesktopClient.Linux.XdgPortal;

[DBusInterface("org.freedesktop.portal.Clipboard")]
public interface IClipboard : IDBusObject
{
  Task<T> GetAsync<T>(string prop);

  Task RequestClipboardAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

  Task<SafeFileHandle> SelectionReadAsync(ObjectPath sessionHandle, string mimeType);

  Task<SafeFileHandle> SelectionWriteAsync(ObjectPath sessionHandle, uint serial);

  Task SelectionWriteDoneAsync(ObjectPath sessionHandle, uint serial, bool success);

  Task SetSelectionAsync(ObjectPath sessionHandle, IDictionary<string, object> options);

  Task<IDisposable> WatchSelectionOwnerChangedAsync(
    Action<(ObjectPath sessionHandle, IDictionary<string, object> options)> handler,
    Action<Exception>? onError = null);

  Task<IDisposable> WatchSelectionTransferAsync(
    Action<(ObjectPath sessionHandle, string mimeType, uint serial)> handler,
    Action<Exception>? onError = null);
}

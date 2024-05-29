namespace ControlR.Viewer.Models.Messages;

internal enum GenericMessageKind
{
    PrivateKeyChanged,
    ServerUriChanged,
    ShuttingDown,
    KeysStateChanged,
    PendingOperationsChanged,
    DevicesCacheUpdated,
    HubConnectionStateChanged,
    DeviceContentWindowsChanged,
    AppUpdateAvailable,
    LocalProxyListenerStopRequested,
    IsServerAdminChanged,
}
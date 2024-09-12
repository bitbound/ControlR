namespace ControlR.Libraries.Clients.Messages;

public enum GenericMessageKind
{
    PrivateKeyChanged,
    ServerUriChanged,
    ShuttingDown,
    KeysStateChanged,
    PendingOperationsChanged,
    DevicesCacheUpdated,
    DeviceContentWindowsChanged,
    LocalProxyListenerStopRequested,
    IsServerAdminChanged,
}
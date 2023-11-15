namespace ControlR.Viewer.Models.Messages;

internal enum GenericMessageKind
{
    PrivateKeyChanged,
    ServerUriChanged,
    ShuttingDown,
    AuthStateChanged,
    PendingOperationsChanged,
    DevicesCacheUpdated,
    HubConnectionStateChanged,
    DeviceContentWindowsChanged,
}
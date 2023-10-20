namespace ControlR.Viewer.Models.Messages;

internal enum ParameterlessMessageKind
{
    PrivateKeyChanged,
    ServerUriChanged,
    ShuttingDown,
    AuthStateChanged,
    PendingOperationsChanged,
    DevicesCacheUpdated,
    HubConnectionStateChanged,
}

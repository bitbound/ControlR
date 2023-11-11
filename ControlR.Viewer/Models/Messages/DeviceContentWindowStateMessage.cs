using ControlR.Viewer.Enums;

namespace ControlR.Viewer.Models.Messages;
internal class DeviceContentWindowStateMessage(Guid windowId, WindowState state)
{
    public Guid WindowId { get; } = windowId;
    public WindowState State { get; } = state;
}

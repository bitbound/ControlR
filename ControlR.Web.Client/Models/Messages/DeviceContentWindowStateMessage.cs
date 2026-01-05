using ControlR.Libraries.Viewer.Common.Enums;

namespace ControlR.Web.Client.Models.Messages;
internal class DeviceContentWindowStateMessage(Guid windowId, WindowState state)
{
  public WindowState State { get; } = state;
  public Guid WindowId { get; } = windowId;
}

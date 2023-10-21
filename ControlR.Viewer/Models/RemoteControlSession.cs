using ControlR.Shared.Dtos;

namespace ControlR.Viewer.Models;

public class RemoteControlSession(DeviceDto device, string sessionPassword)
{
    public DeviceDto Device { get; } = device;
    public Guid SessionId { get; } = Guid.NewGuid();
    public string SessionPassword { get; } = sessionPassword;
}
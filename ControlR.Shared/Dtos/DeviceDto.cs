using ControlR.Shared.Enums;
using ControlR.Shared.Extensions;
using ControlR.Shared.Models;
using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public class DeviceDto : Device
{
    public static Result<DeviceDto> TryCreateFrom(Device device, ConnectionType type, string connectionId)
    {
        var result = device.TryCloneAs<Device, DeviceDto>();
        if (result.IsSuccess)
        {
            result.Value.Type = type;
            result.Value.ConnectionId = connectionId;
        }
        return result;
    }

    [MsgPackKey]
    public ConnectionType Type { get; set; }

    [MsgPackKey]
    public string ConnectionId { get; set; } = string.Empty;
}

using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Models;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

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

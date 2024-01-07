using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public record WakeDeviceDto(
    [property: MsgPackKey] string[] MacAddresses);
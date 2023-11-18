using ControlR.Shared.Dtos;
using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Devices.Common.Messages;

[MessagePackObject]
public record SignedDtoReceivedMessage(
    [property: MsgPackKey] SignedPayloadDto SignedDto);
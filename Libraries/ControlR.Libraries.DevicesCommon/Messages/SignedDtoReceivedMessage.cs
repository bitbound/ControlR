using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.DevicesCommon.Messages;

[MessagePackObject]
public record SignedDtoReceivedMessage(
    [property: MsgPackKey] SignedPayloadDto SignedDto);
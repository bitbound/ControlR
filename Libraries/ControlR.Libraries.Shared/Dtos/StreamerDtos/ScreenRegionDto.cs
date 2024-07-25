using System.Runtime.InteropServices;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ScreenRegionDto(
    [property: MsgPackKey] Guid SessionId,
    [property: MsgPackKey] int X,
    [property: MsgPackKey] int Y,
    [property: MsgPackKey] int Width,
    [property: MsgPackKey] int Height,
    [property: MsgPackKey] byte[] EncodedImage);

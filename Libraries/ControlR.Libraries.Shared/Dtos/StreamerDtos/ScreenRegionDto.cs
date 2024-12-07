namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ScreenRegionDto(
    [property: Key(0)] Guid SessionId,
    [property: Key(1)] int X,
    [property: Key(2)] int Y,
    [property: Key(3)] int Width,
    [property: Key(4)] int Height,
    [property: Key(5)] byte[] EncodedImage);

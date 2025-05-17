namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ScreenRegionDto(
    int X,
    int Y,
    int Width,
    int Height,
    byte[] EncodedImage);

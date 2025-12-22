namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record ScreenRegionDto(
  [property: Key(0)]
  float X,
  [property: Key(1)]
  float Y,
  [property: Key(2)]
  float Width,
  [property: Key(3)]
  float Height,
  [property: Key(4)]
  byte[] EncodedImage);
using System.Buffers;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ScreenRegionDto(
  float X,
  float Y,
  float Width,
  float Height,
  byte[] EncodedImage);
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class DisplayDto
{
  public required PixelSizeDto CapturePixelSize { get; init; }
  public required string DisplayId { get; init; }
  public int Index { get; init; }
  public bool IsPrimary { get; init; }
  public required DisplayBoundsDto LayoutBounds { get; init; }
  public DisplayLayoutCoordinateSpace LayoutCoordinateSpace { get; init; }
  public required string Name { get; init; }
  public PixelSizeDto? NativePixelSize { get; init; }
}
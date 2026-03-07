namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class PixelSizeDto
{
  public int Height { get; init; }

  public int Width { get; init; }
}
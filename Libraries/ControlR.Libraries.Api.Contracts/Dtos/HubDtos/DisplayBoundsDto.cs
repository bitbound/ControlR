namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class DisplayBoundsDto
{
  public double Height { get; init; }

  public double Width { get; init; }

  public double X { get; init; }

  public double Y { get; init; }
}

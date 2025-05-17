namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class DisplayDto
{

    public required string DisplayId { get; init; }

    public double Height { get; init; }

    public bool IsPrimary { get; init; }

    public double Left { get; init; }

    public required string Name { get; init; }

    public double ScaleFactor { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }
}

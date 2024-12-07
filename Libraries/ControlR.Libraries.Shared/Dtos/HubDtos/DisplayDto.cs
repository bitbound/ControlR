namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public class DisplayDto
{

    [Key(nameof(DisplayId))]
    public required string DisplayId { get; init; }

    [Key(nameof(Height))]
    public double Height { get; init; }

    [Key(nameof(IsPrimary))]
    public bool IsPrimary { get; init; }

    [Key(nameof(Left))]
    public double Left { get; init; }

    [Key(nameof(Name))]
    public required string Name { get; init; }

    [Key(nameof(ScaleFactor))]
    public double ScaleFactor { get; init; }

    [Key(nameof(Top))]
    public double Top { get; init; }

    [Key(nameof(Width))]
    public double Width { get; init; }
}

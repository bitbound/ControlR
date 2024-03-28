using ControlR.Shared.Serialization;
using MessagePack;

namespace ControlR.Shared.Dtos;

[MessagePackObject]
public class DisplayDto
{

    [MsgPackKey]
    public string DisplayId { get; init; } = string.Empty;

    [MsgPackKey]
    public int Height { get; init; }

    [MsgPackKey]
    public bool IsPrimary { get; init; }

    [MsgPackKey]
    public int Left { get; init; }

    [MsgPackKey]
    public string MediaId { get; init; } = string.Empty;

    [MsgPackKey]
    public string Name { get; init; } = string.Empty;

    [MsgPackKey]
    public double ScaleFactor { get; init; }

    [MsgPackKey]
    public int Top { get; init; }

    [MsgPackKey]
    public int Width { get; init; }
}

using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class DisplayDto
{

    [MsgPackKey]
    public string DisplayId { get; init; } = string.Empty;

    [MsgPackKey]
    public double Height { get; init; }

    [MsgPackKey]
    public bool IsPrimary { get; init; }

    [MsgPackKey]
    public string Label { get; init; } = string.Empty;

    [MsgPackKey]
    public double Left { get; init; }

    [MsgPackKey]
    public string MediaId { get; init; } = string.Empty;

    [MsgPackKey]
    public string Name { get; init; } = string.Empty;
    [MsgPackKey]
    public double ScaleFactor { get; init; }

    [MsgPackKey]
    public double Top { get; init; }

    [MsgPackKey]
    public double Width { get; init; }
}

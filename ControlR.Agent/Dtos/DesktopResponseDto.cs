using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Agent.Dtos;

[MessagePackObject]
public record DesktopResponseDto([property: MsgPackKey] string DesktopName);
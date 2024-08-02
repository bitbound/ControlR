using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Agent.IpcDtos;

[MessagePackObject]
public record DesktopResponseDto([property: MsgPackKey] string DesktopName);
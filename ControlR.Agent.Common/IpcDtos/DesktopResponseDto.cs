using MessagePack;

namespace ControlR.Agent.Common.IpcDtos;

[MessagePackObject]
public record DesktopResponseDto([property: Key(0)] string DesktopName);
using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Agent.Common.IpcDtos;

[MessagePackObject]
public record DesktopResponseDto([property: MsgPackKey] string DesktopName);
using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record BlockInputResultDto(bool IsSuccess, bool FinalState);

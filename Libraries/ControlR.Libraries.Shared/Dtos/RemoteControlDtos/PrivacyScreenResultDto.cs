using MessagePack;

namespace ControlR.Libraries.Shared.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record PrivacyScreenResultDto(bool IsSuccess, bool FinalState);

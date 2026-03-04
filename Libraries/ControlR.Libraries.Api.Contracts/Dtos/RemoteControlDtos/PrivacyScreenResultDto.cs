using MessagePack;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record PrivacyScreenResultDto(bool IsSuccess, bool FinalState);

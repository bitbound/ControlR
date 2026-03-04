using MessagePack;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record TogglePrivacyScreenDto(bool IsEnabled);

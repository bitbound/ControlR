using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record DisplayDataDto(DisplayDto[] Displays);
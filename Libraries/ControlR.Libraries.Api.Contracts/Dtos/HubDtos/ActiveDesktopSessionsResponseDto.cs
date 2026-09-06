using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record ActiveDesktopSessionsResponseDto(IReadOnlyList<DesktopSession> Sessions);

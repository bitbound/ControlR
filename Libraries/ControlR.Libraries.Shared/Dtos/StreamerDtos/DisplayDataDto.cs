using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Dtos.StreamerDtos;

[MessagePackObject]
public record DisplayDataDto(
    [property: Key(0)] Guid SessionId,
    [property: Key(1)] DisplayDto[] Displays);
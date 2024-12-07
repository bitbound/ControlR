using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record TerminalOutputDto(
    [property: Key(0)] Guid TerminalId,
    [property: Key(1)] string Output,
    [property: Key(2)] TerminalOutputKind OutputKind,
    [property: Key(3)] DateTimeOffset Timestamp);
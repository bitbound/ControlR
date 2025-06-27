using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record TerminalOutputDto(
    Guid TerminalId,
    string Output,
    TerminalOutputKind OutputKind,
    DateTimeOffset Timestamp);
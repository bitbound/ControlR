namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public record TerminalOutputDto(
    Guid TerminalId,
    string Output,
    TerminalOutputKind OutputKind,
    DateTimeOffset Timestamp);
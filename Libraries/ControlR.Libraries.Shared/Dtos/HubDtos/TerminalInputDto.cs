namespace ControlR.Libraries.Shared.Dtos.HubDtos;
[MessagePackObject]
public record TerminalInputDto(
    [property: Key(0)] Guid TerminalId,
    [property: Key(1)] string Input);
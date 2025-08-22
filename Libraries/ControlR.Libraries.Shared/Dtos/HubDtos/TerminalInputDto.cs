namespace ControlR.Libraries.Shared.Dtos.HubDtos;
[MessagePackObject(keyAsPropertyName: true)]
public record TerminalInputDto(
    Guid TerminalId,
    string Input,
    string ViewerConnectionId);
namespace ControlR.Libraries.Shared.Dtos.HubDtos;
[MessagePackObject(keyAsPropertyName: true)]
public record TerminalInputDto(
    Guid TerminalId,
    string Input)
{
  public string? ViewerConnectionId { get; set; }
}
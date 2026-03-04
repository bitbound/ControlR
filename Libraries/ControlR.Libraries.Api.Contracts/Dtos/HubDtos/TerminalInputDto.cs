namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
[MessagePackObject(keyAsPropertyName: true)]
public record TerminalInputDto(
    Guid TerminalId,
    string Input)
{
  public string? ViewerConnectionId { get; set; }
}
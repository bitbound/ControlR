namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

[MessagePackObject]
public record EntityBaseRecordDto(
  [property: Key(0)] Guid Id);
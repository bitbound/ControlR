namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[MessagePackObject]
public record EntityBaseRecordDto(
  [property: Key(0)] Guid Id);
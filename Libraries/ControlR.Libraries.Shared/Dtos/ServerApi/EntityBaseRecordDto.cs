namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public record EntityBaseRecordDto(
  [property: Key(0)] Guid Id);
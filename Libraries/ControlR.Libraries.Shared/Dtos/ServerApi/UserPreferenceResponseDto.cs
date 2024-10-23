namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record UserPreferenceResponseDto(Guid Id, string Name, string Value) : EntityBaseRecordDto(Id);
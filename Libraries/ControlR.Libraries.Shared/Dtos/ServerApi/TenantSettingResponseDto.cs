namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record TenantSettingResponseDto(Guid Id, string Name, string Value) : EntityBaseRecordDto(Id);

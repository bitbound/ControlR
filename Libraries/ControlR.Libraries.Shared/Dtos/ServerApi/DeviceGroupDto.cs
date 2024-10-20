namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record DeviceGroupDto(
  string Name, 
  Guid Id) : EntityBaseRecordDto(Id);
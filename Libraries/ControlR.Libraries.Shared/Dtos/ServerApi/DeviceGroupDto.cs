namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record DeviceGroupDto(
  string Name, 
  int Id, 
  Guid Uid) : EntityBaseRecordDto(Id, Uid);
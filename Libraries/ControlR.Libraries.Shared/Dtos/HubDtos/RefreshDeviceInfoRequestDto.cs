namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record RefreshDeviceInfoRequestDto() : ParameterlessDtoBase(DtoType.RefreshDeviceInfoRequest);
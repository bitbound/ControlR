namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record GetWindowsSessionsDto() : ParameterlessDtoBase(DtoType.GetWindowsSessions);

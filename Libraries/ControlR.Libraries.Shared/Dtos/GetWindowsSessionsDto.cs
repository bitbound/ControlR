namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record GetWindowsSessionsDto() : ParameterlessDtoBase(DtoType.GetWindowsSessions);

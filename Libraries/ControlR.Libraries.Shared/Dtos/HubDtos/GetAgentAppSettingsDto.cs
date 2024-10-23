namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record GetAgentAppSettingsDto() : ParameterlessDtoBase(DtoType.GetAgentAppSettings);

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record CloseStreamingSessionRequestDto() : ParameterlessDtoBase(DtoType.CloseStreamingSession);
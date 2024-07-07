namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record CloseStreamingSessionRequestDto() : ParameterlessDtoBase(DtoType.CloseStreamingSession);
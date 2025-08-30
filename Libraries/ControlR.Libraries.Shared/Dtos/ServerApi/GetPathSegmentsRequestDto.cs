namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record GetPathSegmentsRequestDto(Guid DeviceId, string TargetPath);

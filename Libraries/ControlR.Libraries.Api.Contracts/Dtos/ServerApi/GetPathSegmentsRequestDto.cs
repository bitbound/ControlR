namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record GetPathSegmentsRequestDto(Guid DeviceId, string TargetPath);

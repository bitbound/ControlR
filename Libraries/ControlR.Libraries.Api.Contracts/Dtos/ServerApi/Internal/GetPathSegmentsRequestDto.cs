namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record GetPathSegmentsRequestDto(Guid DeviceId, string TargetPath);

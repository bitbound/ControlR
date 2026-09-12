namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerLogs;

public record GetAspireUrlResponseDto(bool IsConfigured, Uri? AspireUrl);

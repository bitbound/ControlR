namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record DeleteManyDevicesResponseDto(
    IReadOnlyList<Guid> SuccessIds,
    IReadOnlyList<Guid> FailureIds);

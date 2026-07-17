namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record DeleteManyDevicesResponseDto(
    IReadOnlyList<Guid> SuccessIds,
    IReadOnlyList<Guid> FailureIds);

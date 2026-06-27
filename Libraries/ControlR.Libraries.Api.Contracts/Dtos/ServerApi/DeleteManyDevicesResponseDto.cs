namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record DeleteManyDevicesResponseDto(
    IReadOnlyList<Guid> SuccessIds,
    IReadOnlyList<Guid> FailureIds);

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record DeleteManyDevicesResponseDto(
    IReadOnlyList<Guid> SuccessIds,
    IReadOnlyList<Guid> FailureIds);

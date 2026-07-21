namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record UpdateDeviceAliasRequestDto(Guid DeviceId, string? Alias);

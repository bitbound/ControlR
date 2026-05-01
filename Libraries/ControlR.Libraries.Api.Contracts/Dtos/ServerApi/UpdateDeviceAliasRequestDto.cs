namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record UpdateDeviceAliasRequestDto(Guid DeviceId, string? Alias);

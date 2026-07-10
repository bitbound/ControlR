namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

public record UpdateDeviceAliasRequestDto(Guid DeviceId, string? Alias);

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record UpdateDeviceAliasRequestDto(Guid DeviceId, string? Alias);

namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record AgentInstallerKeyUsageDto(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string? RemoteIpAddress);

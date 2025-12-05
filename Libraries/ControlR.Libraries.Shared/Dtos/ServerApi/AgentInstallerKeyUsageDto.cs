namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record AgentInstallerKeyUsageDto(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    string? RemoteIpAddress);

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

public record InstallerKeyUsageDto(
  Guid Id,
  Guid DeviceId,
  DateTimeOffset Timestamp,
  string? RemoteIpAddress);

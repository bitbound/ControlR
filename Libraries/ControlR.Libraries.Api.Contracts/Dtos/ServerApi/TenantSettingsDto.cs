namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public sealed record TenantSettingsDto(
  bool? AppendInstanceId,
  string? InstanceId,
  bool? NotifyUserOnSessionStart,
  Guid? TenantId = null);
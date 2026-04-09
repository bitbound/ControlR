namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public sealed record EffectiveUserPreferencesDto(
  bool NotifyUserOnSessionStart,
  bool IsNotifyUserOnSessionStartTenantEnforced);
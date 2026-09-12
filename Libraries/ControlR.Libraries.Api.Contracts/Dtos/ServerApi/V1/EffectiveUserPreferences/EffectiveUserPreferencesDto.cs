namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.EffectiveUserPreferences;

public sealed record EffectiveUserPreferencesDto(
  bool NotifyUserOnSessionStart,
  bool IsNotifyUserOnSessionStartTenantEnforced);

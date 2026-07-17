namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public sealed record EffectiveUserPreferencesDto(
  bool NotifyUserOnSessionStart,
  bool IsNotifyUserOnSessionStartTenantEnforced);
namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PublicServerSettings;

/// <summary>
/// Server-side settings exposed to any caller, including unauthenticated clients, so a client
/// can adapt to server configuration before authenticating. Never put sensitive or per-tenant
/// data here.
/// </summary>
public record PublicServerSettingsDto(
  bool IsPublicRegistrationEnabled,
  bool DisableDesktopPreview);

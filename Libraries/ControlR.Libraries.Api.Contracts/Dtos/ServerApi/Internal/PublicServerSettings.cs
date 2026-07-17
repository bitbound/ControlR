namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

/// <summary>
/// Server-side settings that are exposed to any caller (including unauthenticated clients)
/// so the UI can adapt to server configuration without requiring a login.
/// </summary>
/// <remarks>
/// Add new entries here when a server-level setting needs to drive client-side behavior
/// (e.g. gating a feature, showing or hiding a control). Avoid putting any sensitive or
/// per-tenant data in this DTO.
/// </remarks>
public record PublicServerSettings(
  bool IsPublicRegistrationEnabled,
  bool DisableDesktopPreview);

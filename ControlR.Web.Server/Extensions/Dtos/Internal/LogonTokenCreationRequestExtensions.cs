using ControlR.Web.Server.Services.LogonTokens;

namespace ControlR.Web.Server.Extensions.Dtos.Internal;

/// <summary>
/// Builds the service-layer <see cref="LogonTokenCreationRequest"/> from the internal request DTO.
/// </summary>
internal static class LogonTokenCreationRequestExtensions
{
  public static LogonTokenCreationRequest ToCreationRequest(
    this InternalDtos.LogonTokenRequestDto request,
    Guid tenantId,
    Guid userId)
  {
    return new LogonTokenCreationRequest(
      DeviceId: request.DeviceId,
      TenantId: tenantId,
      UserId: userId,
      UserCorrelationId: null,
      UserDisplayName: null,
      SessionCorrelationId: null,
      ExpirationMinutes: request.ExpirationMinutes,
      Scopes: request.Scopes is { Count: > 0 } ? [.. request.Scopes] : null,
      AllowedDesktopSessionIds: null);
  }
}
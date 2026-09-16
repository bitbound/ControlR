using ControlR.Web.Server.Services.LogonTokens;

namespace ControlR.Web.Server.Extensions.Dtos.V1;

/// <summary>
/// Builds <see cref="LogonTokenCreationRequest"/> from the V1 request DTOs. Keeps the
/// wire-to-service mapping out of the controllers and in one place.
/// </summary>
internal static class LogonTokenCreationRequestExtensions
{
  public static LogonTokenCreationRequest ToCreationRequest(this V1Dtos.CreateLogonTokenForExternalRequestDto request)
  {
    return new LogonTokenCreationRequest(
      DeviceId: request.DeviceId,
      TenantId: request.TenantId,
      UserId: null,
      UserCorrelationId: request.UserCorrelationId,
      UserDisplayName: request.UserDisplayName,
      SessionCorrelationId: request.SessionCorrelationId,
      ExpirationMinutes: request.ExpirationMinutes,
      Scopes: ToDeviceScopes(request.Permissions, request.DeviceId),
      AllowedDesktopSessionIds: NormalizeDesktopSessionIds(request.AllowedDesktopSessionIds));
  }

  public static LogonTokenCreationRequest ToCreationRequest(this V1Dtos.CreateLogonTokenForUserRequestDto request)
  {
    return new LogonTokenCreationRequest(
      DeviceId: request.DeviceId,
      TenantId: request.TenantId,
      UserId: request.UserId,
      UserCorrelationId: null,
      UserDisplayName: null,
      SessionCorrelationId: request.SessionCorrelationId,
      ExpirationMinutes: request.ExpirationMinutes,
      Scopes: ToDeviceScopes(request.Permissions, request.DeviceId),
      AllowedDesktopSessionIds: NormalizeDesktopSessionIds(request.AllowedDesktopSessionIds));
  }

  private static IReadOnlyList<int>? NormalizeDesktopSessionIds(IReadOnlyList<int>? sessionIds)
  {
    return sessionIds is null ? null : [.. sessionIds.Distinct()];
  }

  private static IReadOnlyList<InternalDtos.CredentialScopeDto>? ToDeviceScopes(
    IReadOnlyList<string>? permissionNames,
    Guid deviceId)
  {
    if (permissionNames is not { Count: > 0 })
    {
      return null;
    }

    return [.. permissionNames.Select(p =>
      new InternalDtos.CredentialScopeDto(p, PermissionScopeKind.Device, deviceId))];
  }
}
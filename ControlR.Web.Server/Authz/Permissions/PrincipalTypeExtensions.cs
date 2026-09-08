namespace ControlR.Web.Server.Authz.Permissions;

public static class PrincipalTypeExtensions
{
  /// <summary>
  /// Maps a <see cref="PrincipalType"/> to the <see cref="AuthorizationChangeLogActorTypes"/>
  /// value used for audit attribution. Unmapped types (e.g. <see cref="PrincipalType.UserGroup"/>)
  /// fall back to <see cref="AuthorizationChangeLogActorTypes.System"/>.
  /// </summary>
  public static string ToAuthorizationChangeLogActorType(this PrincipalType principalType) => principalType switch
  {
    PrincipalType.User => AuthorizationChangeLogActorTypes.User,
    PrincipalType.ServerServiceAccount or PrincipalType.TenantServiceAccount => AuthorizationChangeLogActorTypes.ServiceAccount,
    _ => AuthorizationChangeLogActorTypes.System
  };
}

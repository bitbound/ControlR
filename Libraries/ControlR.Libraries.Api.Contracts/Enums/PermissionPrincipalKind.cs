using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// The kind of principal a permission assignment targets.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionPrincipalKind
{
  User,
  UserGroup,
  ServiceAccount,
  PersonalAccessToken,
  LogonToken
}

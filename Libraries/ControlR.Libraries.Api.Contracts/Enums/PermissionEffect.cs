using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// The effect of a permission assignment. Explicit deny overrides allow at any matching scope.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionEffect
{
  Allow,
  Deny
}

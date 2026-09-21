using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// The kind of principal that created an <c>AgentInstallerKey</c>. This governs ownership and
/// the re-authorization path used when a device re-registers with an existing key: keys created
/// by a <see cref="User"/> are re-checked against that user's current authority, keys created by
/// a <see cref="TenantServiceAccount"/> are re-checked against that account's current authority,
/// and keys created by a <see cref="ServerServiceAccount"/> are authorized by the key itself
/// (server principals are trusted server-wide).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstallerKeyCreatorKind
{
  User = 0,
  ServerServiceAccount = 1,
  TenantServiceAccount = 2
}

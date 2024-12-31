namespace ControlR.Web.Server.Models;

public record AgentInstallerKey(
  Guid TenantId,
  Guid CreatorId,
  string AccessToken,
  InstallerKeyType KeyType,
  uint? AllowedUses,
  DateTimeOffset? Expiration,
  uint CurrentUses = 0);
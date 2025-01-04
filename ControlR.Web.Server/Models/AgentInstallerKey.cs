namespace ControlR.Web.Server.Models;

public record AgentInstallerKey(
  Guid TenantId,
  Guid CreatorId,
  string KeySecret,
  InstallerKeyType KeyType,
  uint? AllowedUses,
  DateTimeOffset? Expiration,
  uint CurrentUses = 0);
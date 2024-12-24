namespace ControlR.Web.Server.Models;

public record AgentInstallerKey(
  Guid TenantId,
  Guid CreatorId,
  string AccessToken);
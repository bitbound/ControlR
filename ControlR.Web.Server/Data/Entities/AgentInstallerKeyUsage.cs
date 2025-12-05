using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class AgentInstallerKeyUsage : TenantEntityBase
{
    public AgentInstallerKey? AgentInstallerKey { get; set; }
    public Guid AgentInstallerKeyId { get; set; }
    public Guid DeviceId { get; set; }
    public string? RemoteIpAddress { get; set; }
}

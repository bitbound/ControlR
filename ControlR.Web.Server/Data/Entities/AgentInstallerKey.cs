using System.ComponentModel.DataAnnotations;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class AgentInstallerKey : TenantEntityBase
{
    public uint? AllowedUses { get; set; }
    public Guid CreatorId { get; set; }
    public DateTimeOffset? Expiration { get; set; }

    [StringLength(200)]
    public string? FriendlyName { get; set; }

    [ProtectedPersonalData]
    public required string HashedKey { get; set; }

    public InstallerKeyType KeyType { get; set; }
    public ICollection<AgentInstallerKeyUsage> Usages { get; set; } = [];
}

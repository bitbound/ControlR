using ControlR.Shared.Models;

namespace ControlR.Server.Models;

public class AppOptions
{
    public IReadOnlyList<IceServer> IceServers { get; init; } = new List<IceServer>();
    public IReadOnlyList<string> AuthorizedAdminIps { get; init; } = new List<string>();
    public IReadOnlyList<string> AuthorizedAdminDnsNames { get; init; } = new List<string>();
}

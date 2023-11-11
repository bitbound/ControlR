using ControlR.Shared.Models;

namespace ControlR.Server.Models;

public class AppOptions
{
    public IReadOnlyList<string> AuthorizedAdminIps { get; init; } = new List<string>();
    public IReadOnlyList<string> AuthorizedAdminDnsNames { get; init; } = new List<string>();
}

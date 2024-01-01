using ControlR.Shared.Models;

namespace ControlR.Server.Models;

public class AppOptions
{
    public bool EnableRestrictedUserAccess { get; init; }
    public IReadOnlyList<string> AuthorizedUserPublicKeys { get; init; } = new List<string>();
    public IReadOnlyList<string> AdminPublicKeys { get; init; } = new List<string>();
}

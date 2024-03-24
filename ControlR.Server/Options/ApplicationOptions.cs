using ControlR.Shared.Models;

namespace ControlR.Server.Options;

public class ApplicationOptions
{
    public const string SectionKey = "ApplicationOptions";
    public IReadOnlyList<IceServer> IceServers { get; init; } = [];
    public IReadOnlyList<string> AdminPublicKeys { get; init; } = [];
    public IReadOnlyList<string> AuthorizedUserPublicKeys { get; init; } = [];
    public string? DockerGatewayIp { get; init; }
    public bool EnableRestrictedUserAccess { get; init; }
    public string[] KnownProxies { get; init; } = [];
    public int LogRetentionDays { get; } = 7;
}
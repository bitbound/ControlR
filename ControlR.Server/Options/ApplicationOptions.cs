namespace ControlR.Server.Options;

public class ApplicationOptions
{
    public const string SectionKey = "ApplicationOptions";
    public IReadOnlyList<string> AdminPublicKeys { get; init; } = [];
    public IReadOnlyList<string> AuthorizedUserPublicKeys { get; init; } = [];
    public string? DockerGatewayIp { get; init; }
    public bool EnableRestrictedUserAccess { get; init; }
    public string[] KnownProxies { get; init; } = [];
    public int LogRetentionDays { get; } = 7;
}
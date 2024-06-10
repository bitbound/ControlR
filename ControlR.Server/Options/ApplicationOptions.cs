using ControlR.Libraries.Shared.Models;

namespace ControlR.Server.Options;

public class ApplicationOptions
{
    public const string SectionKey = "ApplicationOptions";
    public IReadOnlyList<string> AdminPublicKeys { get; init; } = [];
    public IReadOnlyList<string> AuthorizedUserPublicKeys { get; init; } = [];
    public int CoTurnPort { get; init; }
    public string? CoTurnHost { get; init; }
    public string? CoTurnSecret { get; init; }
    public string? CoTurnUsername { get; init; }
    public string? DockerGatewayIp { get; init; }
    public bool EnableRestrictedUserAccess { get; init; }
    public bool EnableStoreIntegration { get; init; }
    public IReadOnlyList<IceServer> IceServers { get; init; } = [];
    public string[] KnownProxies { get; init; } = [];
    public int LogRetentionDays { get; } = 7;
    public string? MeteredApiKey { get; init; }
    public string? TwilioSecret { get; init; }
    public string? TwilioSid { get; init; }
    public bool UseCoTurn { get; init; }
    public bool UseMetered { get; init; }
    public bool UseRedisBackplane { get; init; }
    public bool UseStaticIceServers { get; init; }
    public bool UseTwilio { get; init; }
}
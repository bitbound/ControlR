namespace ControlR.Web.Server.Options;

public class AppOptions
{
  public const string SectionKey = "AppOptions";
  public bool AllowAgentsToSelfBootstrap { get; init; }
  public string? DockerGatewayIp { get; init; }
  public bool EnableCloudflareProxySupport { get; init; }
  public bool EnablePublicRegistration { get; init; }
  public IReadOnlyList<ExternalWebSocketHost> ExternalWebSocketHosts { get; init; } = [];
  public string? GitHubClientId { get; init; }
  public string? GitHubClientSecret { get; init; }
  public string? InMemoryDatabaseName { get; init; }
  public string[] KnownNetworks { get; init; } = [];
  public string[] KnownProxies { get; init; } = [];
  public string? MicrosoftClientId { get; init; }
  public string? MicrosoftClientSecret { get; init; }
  public bool RequireUserEmailConfirmation { get; init; }
  public Uri? ServerBaseUri { get; init; }
  public bool SmtpCheckCertificateRevocation { get; init; } = true;
  public string? SmtpDisplayName { get; init; }
  public string? SmtpEmail { get; init; }
  public string? SmtpHost { get; init; }
  public string? SmtpLocalDomain { get; init; }
  public string? SmtpPassword { get; init; }
  public int SmtpPort { get; init; } = 587;
  public string? SmtpUserName { get; init; }
  public bool UseExternalWebSocketRelay { get; init; }
  public bool UseHttpLogging { get; init; }
  public bool UseInMemoryDatabase { get; init; }
}
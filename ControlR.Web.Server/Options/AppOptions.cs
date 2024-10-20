namespace ControlR.Web.Server.Options;

public class AppOptions
{
  public const string SectionKey = "AppOptions";
  public string? DockerGatewayIp { get; init; }
  public bool EnablePublicRegistration { get; init; }
  public IReadOnlyList<ExternalWebSocketHost> ExternalWebSocketHosts { get; init; } = [];
  public string[] KnownProxies { get; init; } = [];
  public int LogRetentionDays { get; } = 7;
  public bool RequireUserEmailConfirmation { get; init; }
  public bool SmtpCheckCertificateRevocation { get; set; } = true;
  public string? SmtpDisplayName { get; set; }
  public string? SmtpEmail { get; set; }
  public string? SmtpHost { get; set; }
  public string? SmtpLocalDomain { get; set; }
  public string? SmtpPassword { get; set; }
  public int SmtpPort { get; set; } = 587;
  public string? SmtpUserName { get; set; }
  public bool UseExternalWebSocketBridge { get; init; }
  public bool UseRedisBackplane { get; init; }
}
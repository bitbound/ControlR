namespace ControlR.Web.Server.Options;

public class ApplicationOptions
{
  public const string SectionKey = "ApplicationOptions";
  public string? DockerGatewayIp { get; init; }
  public bool EnablePublicRegistration { get; init; }
  public IReadOnlyList<ExternalWebSocketHost> ExternalWebSocketHosts { get; init; } = [];
  public string[] KnownProxies { get; init; } = [];
  public int LogRetentionDays { get; } = 7;
  public bool UseExternalWebSocketBridge { get; init; }
  public bool UseRedisBackplane { get; init; }
}
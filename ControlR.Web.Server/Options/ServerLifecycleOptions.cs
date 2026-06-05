namespace ControlR.Web.Server.Options;

/// <summary>
/// Server lifecycle configuration options for ControlR web server.
/// </summary>
public class ServerLifecycleOptions
{
  /// <summary>
  /// The configuration section key for ServerLifecycleOptions in appsettings.json.
  /// </summary>
  public const string SectionKey = "ServerLifecycle";

  /// <summary>
  /// When true, agents will automatically uninstall themselves when they connect.
  /// </summary>
  public bool DecommissionServer { get; set; }
}

namespace ControlR.Web.Server.Options;

/// <summary>
/// Configuration for development and load-testing conveniences that must never be enabled on a server
/// reachable by users.
/// </summary>
/// <remarks>
/// These settings trade away guarantees the rest of the product is built on, so they live apart from
/// <see cref="AppOptions"/> where an operator can mistake one for a convenience toggle. Every one of
/// them is reported at startup when it is on.
/// </remarks>
public class DeveloperOptions
{
  /// <summary>
  /// The configuration section key for DeveloperOptions in appsettings.json.
  /// </summary>
  public const string SectionKey = "DeveloperOptions";

  /// <summary>
  /// Lets agents register themselves without an installer key.
  /// </summary>
  /// <remarks>
  /// <para>
  /// For development and load testing only. Never enable on a production server.
  /// </para>
  /// <para>
  /// The agent hub authenticates a device by the public key stored against its id. Adopting a key for a
  /// device the server has never seen is only safe while the caller already holds an installer key, so
  /// this setting moves that check onto the trust of the caller instead. Turning it on changes the
  /// security posture of the whole hub surface, and it is the precondition for every finding filed
  /// against anonymous hub writes.
  /// </para>
  /// </remarks>
  public bool AllowAgentsToSelfBootstrap { get; init; }
}

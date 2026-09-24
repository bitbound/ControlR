namespace ControlR.Web.Server.Options;

/// <summary>
/// Switches for development and load testing only. Never enable any of them on a production server.
/// </summary>
/// <remarks>
/// Separate from <see cref="AppOptions"/> so nobody mistakes one of these for a support toggle.
/// </remarks>
public class DeveloperOptions
{
  /// <summary>
  /// Section name in appsettings.json.
  /// </summary>
  public const string SectionKey = "DeveloperOptions";

  /// <summary>
  /// <para>
  /// For development and load testing only. Never enable on a production server.
  /// </para>
  /// <para>
  /// Lets an unknown agent enroll itself without an installer key.
  /// </para>
  /// </summary>
  /// <remarks>
  /// A device is normally registered before it can use the hub, and the hub then trusts only the public
  /// key stored for it. With this on, a device the server has never seen can send its own key and be
  /// believed. The server warns at startup when this is enabled.
  /// </remarks>
  public bool AllowAgentsToSelfBootstrap { get; init; }
}

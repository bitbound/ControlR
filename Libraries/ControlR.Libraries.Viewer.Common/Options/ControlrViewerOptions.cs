namespace ControlR.Libraries.Viewer.Common.Options;

/// <summary>
///   Required options for configuring a ControlrViewer instance.
/// </summary>
public class ControlrViewerOptions
{
  /// <summary>
  ///   Determines how the viewer authenticates against the ControlR server.
  /// </summary>
  public required ViewerAuthenticationMethod AuthenticationMethod { get; set; }
  /// <summary>
  ///   The base URL of the ControlR server to which the viewer will connect (e.g. "https://controlr.example.com").
  /// </summary>
  public required Uri BaseUrl { get; set; }
  /// <summary>
  ///   The device ID that the viewer will be accessing.
  /// </summary>
  public required Guid DeviceId { get; set; }
  /// <summary>
  ///   A static personal access token used when <see cref="AuthenticationMethod"/> is <see cref="ViewerAuthenticationMethod.PersonalAccessToken"/>.
  /// </summary>
  public string? PersonalAccessToken { get; set; }
}
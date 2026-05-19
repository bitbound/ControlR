using ControlR.ApiClient;

namespace ControlR.Libraries.Viewer.Common.Options;

/// <summary>
///   Required options for configuring a ControlrViewer instance.
/// </summary>
public class ControlrViewerOptions
{
  /// <summary>
  ///   Authentication settings for the current user.
  /// </summary>
  public ControlrApiClientAuthOptions Auth { get; set; } = new();
  /// <summary>
  ///   The base URL of the ControlR server to which the viewer will connect (e.g. "https://controlr.example.com").
  /// </summary>
  public required Uri BaseUrl { get; set; }
  public string? BearerToken
  {
    get => Auth.BearerToken;
    set => Auth.BearerToken = value;
  }
  /// <summary>
  ///   The device ID that the viewer will be accessing.
  /// </summary>
  public required Guid DeviceId { get; set; }
  public string? PersonalAccessToken
  {
    get => Auth.PersonalAccessToken;
    set => Auth.PersonalAccessToken = value;
  }
}
using ControlR.Libraries.DataRedaction;
using Microsoft.Extensions.Compliance.Classification;

namespace ControlR.ApiClient;

/// <summary>
/// Options for configuring the ControlR API client.
/// </summary>
public class ControlrApiClientOptions
{
  /// <summary>
  /// Gets or sets the base URI where the ControlR API is hosted.
  /// </summary>
  /// <remarks>The base URI typically specifies the root endpoint for all API calls. Ensure that the URI is
  /// absolute and includes the appropriate scheme (such as "https").</remarks>
  public required Uri BaseUrl { get; set; }

  /// <summary>
  /// Gets or sets the personal access token used for authenticating API requests.
  /// </summary>
  /// <remarks>The personal access token must have sufficient permissions for the operations being performed.
  /// Store and handle this token securely to prevent unauthorized access.</remarks>
  [ProtectedDataClassification]
  public required string PersonalAccessToken { get; set; }

  public override string ToString() => $"BaseUrl: {BaseUrl}, PersonalAccessToken: [REDACTED]";
}

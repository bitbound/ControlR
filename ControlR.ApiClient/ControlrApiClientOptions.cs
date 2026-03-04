using ControlR.Libraries.DataRedaction;
using Microsoft.Extensions.Compliance.Classification;

namespace ControlR.ApiClient;

/// <summary>
/// Options for configuring the ControlR API client.
/// </summary>
public class ControlrApiClientOptions
{
  /// <summary>
  /// The name of the HTTP header used to send the personal access token (PAT) for authentication with the ControlR API.
  /// </summary>
  public const string PersonalAccessTokenHeader = "x-personal-token";
  /// <summary>
  /// The default configuration section key for ControlR API client options.
  /// </summary>
  public const string SectionKey = "ControlrApiClient";

  /// <summary>
  /// The base URI where the ControlR server is hosted (e.g. https://controlr.example.com).
  /// </summary>
  public required Uri BaseUrl { get; set; }
  
  /// <summary>
  /// When <c>false</c> (default), response DTOs are validated for nullability/required-member contract violations
  /// and invalid responses are returned as failed <c>ApiResult</c> values.
  /// When <c>true</c>, response DTO validation failures are logged as warnings and successful responses are still returned.
  /// </summary>
  public bool DisableResponseDtoStrictness { get; set; }

  /// <summary>
  /// When <c>false</c> (default), streamed response DTOs (for example from async enumerable endpoints)
  /// are validated item-by-item using the same DTO strictness rules.
  /// When <c>true</c>, streamed response DTO validation is disabled.
  /// </summary>
  public bool DisableStreamingResponseDtoStrictness { get; set; }
  
  /// <summary>
  /// If supplied, the client will include the personal access token (PAT) in the "x-personal-token" header of each request.
  /// A PAT is generated in the ControlR UI and can be used to authenticate API requests.
  /// </summary>
  [ProtectedDataClassification]
  public string? PersonalAccessToken { get; set; }

  public override string ToString() => $"BaseUrl: {BaseUrl}, PersonalAccessToken: [REDACTED]";
}

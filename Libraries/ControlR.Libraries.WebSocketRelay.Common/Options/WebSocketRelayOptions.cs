namespace ControlR.Libraries.WebSocketRelay.Common.Options;

/// <summary>
/// Options for WebSocket Relay middleware.
/// </summary>
public class WebSocketRelayOptions
{
  /// <summary>
  /// Authorization policy for requester connections.
  /// </summary>
  public string? AuthorizationPolicyForRequester { get; set; }

  /// <summary>
  /// Authorization policy for responder connections.
  /// </summary>
  public string? AuthorizationPolicyForResponder { get; set; }

  /// <summary>
  /// Longest a connection may wait for its partner, however long it asks to wait.
  /// </summary>
  public TimeSpan MaxWaitForPartnerTimeout { get; set; } = TimeSpan.FromSeconds(120);

  /// <summary>
  /// Whether to require authentication for requester connections.
  /// </summary>
  public bool RequireAuthenticationForRequester { get; set; }

  /// <summary>
  /// Whether to require authentication for responder connections.
  /// </summary>
  public bool RequireAuthenticationForResponder { get; set; }
}

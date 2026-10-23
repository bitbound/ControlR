namespace ControlR.ApiClient.Internal;

/// <summary>
/// Carries the bearer credential the auth header handler applied to one API call's request, so the
/// retry decision can key on the token the server actually rejected.
/// </summary>
/// <remarks>
/// The handler receives a copy of the caller's request message and the response's own request reference
/// does not carry options set on that copy, so the credential cannot travel on the message. It travels
/// on the async flow instead: <see cref="ControlrApi"/> opens a scope, the handler records into it, and
/// the catch filter reads it. A call that recorded no credential cannot buy a retry, which is what keeps
/// an anonymous or pre-sign-in request from being replayed under an identity that appeared mid-flight.
/// </remarks>
internal sealed class BearerAttempt
{
  private static readonly AsyncLocal<BearerAttempt?> _current = new();

  /// <summary>
  /// The scope the current async flow is sending under. Reads return null outside a call.
  /// </summary>
  public static BearerAttempt? Current
  {
    get => _current.Value;
    set => _current.Value = value;
  }

  /// <summary>
  /// The bearer credential applied to the most recent request sent under this scope, or
  /// <see langword="null"/> when the request carried none.
  /// </summary>
  public BearerCredential? SentBearer { get; set; }

  /// <summary>
  /// Whether <paramref name="currentBearer"/> is a renewal of the credential the failed request carried.
  /// </summary>
  public bool CanRetryWith(BearerCredential? currentBearer) =>
    SentBearer is not null &&
    currentBearer is not null &&
    !ReferenceEquals(currentBearer, SentBearer);
}

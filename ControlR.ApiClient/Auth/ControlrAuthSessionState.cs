namespace ControlR.ApiClient.Auth;

/// <summary>
/// The state of an interactive auth session.
/// </summary>
/// <remarks>
/// The values are explicit so that reordering the members, by hand or by a member-ordering analyzer,
/// cannot silently renumber them. Code compiled against an older assembly compares against the
/// numeric value, so a reorder without explicit values quietly changes what shipped code means.
/// </remarks>
public enum ControlrAuthSessionState
{
  SignedOut = 0,
  PatConfigured = 1,
  ServiceAccountConfigured = 2,
  AwaitingPasswordChange = 3,
  AwaitingTwoFactor = 4,
  Authenticated = 5,
  Expired = 6,

  // Terminal. The session is dead and cannot authenticate again, so the caller needs a new session
  // rather than a sign-in on this one. Distinct from Expired, which invites a retry on the same
  // object and would send a subscriber into a sign-in flow that cannot succeed.
  Disposed = 7
}

namespace ControlR.ApiClient.Auth;

public enum ControlrAuthSessionState
{
  SignedOut,
  PatConfigured,
  AwaitingPasswordChange,
  AwaitingTwoFactor,
  Authenticated,
  Expired,

  // Appended rather than placed next to PatConfigured so the numeric value of every pre-existing
  // member is unchanged.
  ServiceAccountConfigured
}
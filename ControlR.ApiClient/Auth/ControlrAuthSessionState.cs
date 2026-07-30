namespace ControlR.ApiClient.Auth;

public enum ControlrAuthSessionState
{
  SignedOut,
  PatConfigured,
  AwaitingPasswordChange,
  AwaitingTwoFactor,
  Authenticated,
  Expired
}
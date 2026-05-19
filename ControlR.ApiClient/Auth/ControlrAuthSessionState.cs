namespace ControlR.ApiClient.Auth;

public enum ControlrAuthSessionState
{
  SignedOut,
  AwaitingTwoFactor,
  Authenticated,
  Expired
}
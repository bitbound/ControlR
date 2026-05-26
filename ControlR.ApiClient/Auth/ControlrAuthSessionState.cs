namespace ControlR.ApiClient.Auth;

public enum ControlrAuthSessionState
{
  SignedOut,
  AwaitingPasswordChange,
  AwaitingTwoFactor,
  Authenticated,
  Expired
}
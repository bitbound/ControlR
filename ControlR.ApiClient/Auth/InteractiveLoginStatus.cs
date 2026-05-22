namespace ControlR.ApiClient.Auth;

public enum InteractiveLoginStatus
{
  Failed,
  LockedOut,
  RequiresTwoFactor,
  Authenticated
}
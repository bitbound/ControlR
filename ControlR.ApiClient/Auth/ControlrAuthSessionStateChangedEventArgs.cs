namespace ControlR.ApiClient.Auth;

public sealed class ControlrAuthSessionStateChangedEventArgs(
  ControlrAuthSessionState state,
  string? message = null) : EventArgs
{
  public string? Message { get; } = message;
  public ControlrAuthSessionState State { get; } = state;
}
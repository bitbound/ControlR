namespace ControlR.Agent.Shared.Services.Linux;

/// <summary>
/// A logged-in user's session on the machine, with the facts needed to decide what a caller may do with it.
/// </summary>
/// <param name="Uid">Numeric user id as a string.</param>
/// <param name="SessionType">The logind <c>Type</c> value (for example x11, wayland, or tty).</param>
/// <param name="IsActive">Whether the session is the active one at its seat.</param>
public sealed record LoggedInUserSession(string Uid, string SessionType, bool IsActive)
{
  /// <summary>
  /// Whether the session can display a GUI (X11 or Wayland).
  /// </summary>
  public bool IsGraphical =>
    SessionType.Equals("x11", StringComparison.OrdinalIgnoreCase) ||
    SessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase);
}

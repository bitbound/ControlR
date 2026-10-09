namespace ControlR.Agent.Shared.Models;

/// <summary>
/// How an install attempt ended when it did not fail. A handoff is not a failure. The installer was
/// started from inside its own install directory, so it copied itself to a temp directory and started
/// that copy, and the copy owns the result. A caller must not roll back or remove anything on a
/// handoff, because the copy it just started is still doing the work.
/// </summary>
public enum AgentInstallOutcome
{
  HandedOff,
  Installed
}

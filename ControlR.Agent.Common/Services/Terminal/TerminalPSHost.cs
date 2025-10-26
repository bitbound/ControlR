using System.Globalization;
using System.Management.Automation.Host;

namespace ControlR.Agent.Common.Services.Terminal;

// Custom PowerShell Host implementation for interactive scenarios
internal class TerminalPSHost : PSHost
{
  private readonly Guid _instanceId = Guid.NewGuid();
  private readonly TerminalSession _terminalSession;
  private readonly TerminalHostUI _ui;

  public TerminalPSHost(TerminalSession terminalSession)
  {
    _terminalSession = terminalSession;
    _ui = new TerminalHostUI(_terminalSession);
  }

  public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;

  public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;

  public override Guid InstanceId => _instanceId;

  public override string Name => "ControlR Terminal Host";

  public override PSHostUserInterface UI => _ui;

  public override Version Version => new(1, 0, 0, 0);

  public override void EnterNestedPrompt()
  {
    // Not implemented for this scenario
  }

  public override void ExitNestedPrompt()
  {
    // Not implemented for this scenario
  }

  public override void NotifyBeginApplication()
  {
    // Not needed for this implementation
  }

  public override void NotifyEndApplication()
  {
    // Not needed for this implementation
  }

  public override void SetShouldExit(int exitCode)
  {
    _terminalSession.TriggerProcessExited();
  }
}

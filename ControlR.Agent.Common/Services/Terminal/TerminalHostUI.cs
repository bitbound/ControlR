using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;

namespace ControlR.Agent.Common.Services.Terminal;

// Custom PowerShell Host UI for handling interactive input/output
internal class TerminalHostUI : PSHostUserInterface
{
  private readonly TerminalSession _terminalSession;
  private readonly TerminalRawUI _rawUI;

  public TerminalHostUI(TerminalSession terminalSession)
  {
    _terminalSession = terminalSession;
    _rawUI = new TerminalRawUI();
  }

  public override PSHostRawUserInterface RawUI => _rawUI;

  public override string ReadLine()
  {
    // This handles Read-Host scenarios
    return _terminalSession.HandleHostReadLine().Result;
  }

  public override SecureString ReadLineAsSecureString()
  {
    // For password input - would need special handling
    var input = ReadLine();
    var secureString = new SecureString();
    foreach (char c in input)
    {
      secureString.AppendChar(c);
    }
    secureString.MakeReadOnly();
    return secureString;
  }

  public override void Write(string value)
  {
    _ = Task.Run(() => _terminalSession.SendOutput(value, TerminalOutputKind.StandardOutput));
  }

  public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
  {
    Write(value); // Ignore colors for now
  }

  public override void WriteLine(string value)
  {
    Write(value + Environment.NewLine);
  }

  public override void WriteErrorLine(string value)
  {
    _ = Task.Run(() => _terminalSession.SendOutput(value + Environment.NewLine, TerminalOutputKind.StandardError));
  }

  public override void WriteDebugLine(string message)
  {
    Write($"DEBUG: {message}{Environment.NewLine}");
  }

  public override void WriteProgress(long sourceId, ProgressRecord record)
  {
    Write($"[{record.PercentComplete}%] {record.Activity}: {record.StatusDescription}{Environment.NewLine}");
  }

  public override void WriteVerboseLine(string message)
  {
    Write($"VERBOSE: {message}{Environment.NewLine}");
  }

  public override void WriteWarningLine(string message)
  {
    Write($"WARNING: {message}{Environment.NewLine}");
  }

  public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
  {
    // Handle complex prompts - simplified implementation
    _ = Task.Run(() => _terminalSession.HandleHostPrompt($"{caption}: {message}"));
    return new Dictionary<string, PSObject>();
  }

  public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
  {
    // Simplified credential prompt
    _ = Task.Run(() => _terminalSession.HandleHostPrompt($"Credential required: {caption} - {message}"));
    return new PSCredential("user", new SecureString());
  }

  public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
  {
    return PromptForCredential(caption, message, userName, targetName);
  }

  public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
  {
    // Handle choice prompts
    _ = Task.Run(() => _terminalSession.HandleHostPrompt($"{caption}: {message}"));
    for (int i = 0; i < choices.Count; i++)
    {
      _ = Task.Run(() => _terminalSession.HandleHostPrompt($"[{i}] {choices[i].Label}: {choices[i].HelpMessage}"));
    }
    return defaultChoice;
  }
}

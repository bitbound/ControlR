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
    return ReadLineAsync().GetAwaiter().GetResult();
  }

  private async Task<string> ReadLineAsync()
  {
    return await _terminalSession.HandleHostReadLine();
  }

  public override SecureString ReadLineAsSecureString()
  {
    // For password input
    var input = ReadLineAsync().GetAwaiter().GetResult();
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
    return PromptAsync(caption, message, descriptions).Result;
  }

  private async Task<Dictionary<string, PSObject>> PromptAsync(string caption, string message, Collection<FieldDescription> descriptions)
  {
    // Send the prompt header
    if (!string.IsNullOrEmpty(caption))
    {
      await _terminalSession.HandleHostPrompt($"{caption}");
    }
    if (!string.IsNullOrEmpty(message))
    {
      await _terminalSession.HandleHostPrompt($"{message}");
    }

    var results = new Dictionary<string, PSObject>();

    // Handle each field description
    foreach (var field in descriptions)
    {
      await _terminalSession.HandleHostPrompt($"{field.Label}: ");
      var input = await _terminalSession.HandleHostReadLine();
      
      // Convert to appropriate type based on field attributes
      if (field.ParameterTypeName.Equals("SecureString", StringComparison.OrdinalIgnoreCase) || 
          field.Label.ToLower().Contains("password"))
      {
        var secureString = new SecureString();
        foreach (char c in input)
        {
          secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        results[field.Name] = new PSObject(secureString);
      }
      else
      {
        results[field.Name] = new PSObject(input);
      }
    }

    return results;
  }

  public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
  {
    return PromptForCredentialAsync(caption, message, userName, targetName).Result;
  }

  private async Task<PSCredential> PromptForCredentialAsync(string caption, string message, string userName, string targetName)
  {
    // Send the credential prompt
    await _terminalSession.HandleHostPrompt($"Credential required: {caption} - {message}");
    
    // Prompt for username if not provided
    if (string.IsNullOrEmpty(userName))
    {
      await _terminalSession.HandleHostPrompt("User: ");
      userName = await _terminalSession.HandleHostReadLine();
    }
    
    // Prompt for password
    await _terminalSession.HandleHostPrompt("Password: ");
    var password = await _terminalSession.HandleHostReadLine();
    
    // Convert password to SecureString
    var securePassword = new SecureString();
    foreach (char c in password)
    {
      securePassword.AppendChar(c);
    }
    securePassword.MakeReadOnly();
    
    return new PSCredential(userName, securePassword);
  }

  public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
  {
    return PromptForCredential(caption, message, userName, targetName);
  }

  public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
  {
    return PromptForChoiceAsync(caption, message, choices, defaultChoice).Result;
  }

  private async Task<int> PromptForChoiceAsync(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
  {
    // Send the choice prompt
    if (!string.IsNullOrEmpty(caption))
    {
      await _terminalSession.HandleHostPrompt($"{caption}");
    }
    if (!string.IsNullOrEmpty(message))
    {
      await _terminalSession.HandleHostPrompt($"{message}");
    }

    // Display choices
    for (int i = 0; i < choices.Count; i++)
    {
      var choiceText = $"[{i}] {choices[i].Label}";
      if (!string.IsNullOrEmpty(choices[i].HelpMessage))
      {
        choiceText += $": {choices[i].HelpMessage}";
      }
      if (i == defaultChoice)
      {
        choiceText += " (default)";
      }
      await _terminalSession.HandleHostPrompt(choiceText);
    }

    // Prompt for choice
    await _terminalSession.HandleHostPrompt($"Choice [0-{choices.Count - 1}] (default is {defaultChoice}): ");
    var input = await _terminalSession.HandleHostReadLine();

    // Parse the choice
    if (string.IsNullOrWhiteSpace(input))
    {
      return defaultChoice;
    }

    if (int.TryParse(input.Trim(), out int choice) && choice >= 0 && choice < choices.Count)
    {
      return choice;
    }

    // Invalid choice, return default
    return defaultChoice;
  }
}

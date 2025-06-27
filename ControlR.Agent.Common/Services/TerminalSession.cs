using System.Diagnostics;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;
using System.Security;
using System.Globalization;

namespace ControlR.Agent.Common.Services;

public interface ITerminalSession : IDisposable
{
  bool IsDisposed { get; }

  TerminalSessionKind SessionKind { get; }
  event EventHandler? ProcessExited;

  Task<Result> WriteInput(string input, TimeSpan timeout);
}

internal class TerminalSession(
  Guid terminalId,
  string viewerConnectionId,
  TimeProvider timeProvider,
  ISystemEnvironment environment,
  IHubConnection<IAgentHub> hubConnection,
  ILogger<TerminalSession> logger) : ITerminalSession
{
  private readonly SemaphoreSlim _writeLock = new(1, 1);
  private readonly string _viewerConnectionId = viewerConnectionId;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly ISystemEnvironment _environment = environment;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<TerminalSession> _logger = logger;
  private PowerShell? _powerShell;
  private Runspace? _runspace;
  private TerminalPSHost? _psHost;

  public Guid TerminalId { get; } = terminalId;

  public event EventHandler? ProcessExited;

  public bool IsDisposed { get; private set; }

  public TerminalSessionKind SessionKind { get; private set; } = TerminalSessionKind.PowerShell;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  public async Task<Result> WriteInput(string input, TimeSpan timeout)
  {
    await _writeLock.WaitAsync();

    try
    {
      if (_powerShell == null || _runspace?.RunspaceStateInfo.State != RunspaceState.Opened)
      {
        throw new InvalidOperationException("PowerShell session is not running.");
      }

      using var cts = new CancellationTokenSource(timeout);

      if (!string.IsNullOrWhiteSpace(input))
      {
        // Clear any previous commands and add the new input
        _powerShell.Commands.Clear();
        _powerShell.AddScript(input);

        // Execute the command asynchronously
        var results = await Task.Run(() => _powerShell.Invoke(), cts.Token);

        // Handle any errors
        if (_powerShell.HadErrors)
        {
          foreach (var error in _powerShell.Streams.Error)
          {
            await SendOutput(error.ToString(), TerminalOutputKind.StandardError);
          }
        }

        // Send results to output (PowerShell will handle prompt automatically)
        foreach (var result in results)
        {
          await SendOutput(result?.ToString() ?? string.Empty, TerminalOutputKind.StandardOutput);
        }
      }

      return Result.Ok();
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogError(ex, "Timed out while executing PowerShell command.");
      return Result.Fail("Command execution timed out.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while executing PowerShell command.");
      return Result.Fail("An error occurred during command execution.");
    }
    finally
    {
      _writeLock.Release();
    }
  }

  internal async Task Initialize()
  {
    try
    {
      // Create custom PowerShell host for interactive scenarios
      _psHost = new TerminalPSHost(this);
      
      // Create runspace with custom host
      _runspace = RunspaceFactory.CreateRunspace(_psHost);
      _runspace.Open();

      // Set working directory
      _runspace.SessionStateProxy.Path.SetLocation(_environment.StartupDirectory);

      // Create PowerShell instance
      _powerShell = PowerShell.Create();
      _powerShell.Runspace = _runspace;

      // Set up event handlers
      _runspace.StateChanged += Runspace_StateChanged;

      // Send initial prompt
      await SendOutput($"PowerShell {PSVersionInfo.PSVersion} on {Environment.OSVersion}", TerminalOutputKind.StandardOutput);
      await SendOutput($"Working Directory: {_environment.StartupDirectory}", TerminalOutputKind.StandardOutput);
      await SendOutput("Type 'bash' to start bash, 'zsh' for zsh, or use PowerShell commands.", TerminalOutputKind.StandardOutput);
      
      // Send initial prompt
      await SendPrompt();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error initializing PowerShell session.");
      throw;
    }
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!IsDisposed)
    {
      if (disposing)
      {
        _powerShell?.Dispose();
        _runspace?.Dispose();
      }

      IsDisposed = true;
    }
  }

  private void Runspace_StateChanged(object? sender, RunspaceStateEventArgs e)
  {
    if (e.RunspaceStateInfo.State == RunspaceState.Closed || 
        e.RunspaceStateInfo.State == RunspaceState.Broken)
    {
      ProcessExited?.Invoke(this, EventArgs.Empty);
    }
  }

  public async Task SendOutput(string output, TerminalOutputKind outputKind)
  {
    try
    {
      var outputDto = new TerminalOutputDto(
        TerminalId,
        output,
        outputKind,
        _timeProvider.GetLocalNow());

      await _hubConnection.Server.SendTerminalOutputToViewer(_viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal output.");
    }
  }

  private async Task SendPrompt()
  {
    try
    {
      var location = _runspace?.SessionStateProxy.Path.CurrentLocation?.Path ?? _environment.StartupDirectory;
      var prompt = $"PS {Environment.UserName}@{Environment.MachineName}:{location}> ";
      await SendOutput(prompt, TerminalOutputKind.StandardOutput);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending prompt.");
    }
  }

  public async Task HandleHostPrompt(string prompt)
  {
    await SendOutput(prompt, TerminalOutputKind.StandardOutput);
  }

  public async Task<string> HandleHostReadLine()
  {
    // This will be called by PowerShell when it needs input (like Read-Host)
    // We'll need to implement a mechanism to wait for user input from SignalR
    await SendOutput("[Waiting for input...]", TerminalOutputKind.StandardOutput);
    
    // For now, return empty string - in a full implementation, you'd want to
    // set up a mechanism to wait for the next SignalR message and return that
    return string.Empty;
  }

  public void TriggerProcessExited()
  {
    ProcessExited?.Invoke(this, EventArgs.Empty);
  }
}

// Custom PowerShell Host implementation for interactive scenarios
internal class TerminalPSHost : PSHost
{
  private readonly TerminalSession _terminalSession;
  private readonly TerminalHostUI _ui;
  private readonly Guid _instanceId = Guid.NewGuid();

  public TerminalPSHost(TerminalSession terminalSession)
  {
    _terminalSession = terminalSession;
    _ui = new TerminalHostUI(_terminalSession);
  }

  public override string Name => "ControlR Terminal Host";

  public override Version Version => new(1, 0, 0, 0);

  public override Guid InstanceId => _instanceId;

  public override PSHostUserInterface UI => _ui;

  public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;

  public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;

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

// Minimal RawUI implementation
internal class TerminalRawUI : PSHostRawUserInterface
{
  public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
  public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
  public override Coordinates CursorPosition { get; set; }
  public override int CursorSize { get; set; } = 25;
  public override Size BufferSize { get; set; } = new(120, 30);
  public override Size MaxPhysicalWindowSize => new(120, 30);
  public override Size MaxWindowSize => new(120, 30);
  public override Coordinates WindowPosition { get; set; }
  public override Size WindowSize { get; set; } = new(120, 30);
  public override string WindowTitle { get; set; } = "ControlR Terminal";
  public override bool KeyAvailable => false;

  public override void FlushInputBuffer() { }
  public override BufferCell[,] GetBufferContents(Rectangle rectangle) => new BufferCell[rectangle.Bottom - rectangle.Top + 1, rectangle.Right - rectangle.Left + 1];
  public override KeyInfo ReadKey(ReadKeyOptions options) => new();
  public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill) { }
  public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) { }
  public override void SetBufferContents(Rectangle rectangle, BufferCell fill) { }
}
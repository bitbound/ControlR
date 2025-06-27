using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace ControlR.Agent.Common.Services.Terminal;

public interface ITerminalSession : IDisposable
{
  bool IsDisposed { get; }

  TerminalSessionKind SessionKind { get; }
  event EventHandler? ProcessExited;

  Task<Result> WriteInput(string input, CancellationToken cancellationToken);
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
  private TaskCompletionSource<string>? _pendingInputRequest;

  public Guid TerminalId { get; } = terminalId;

  public event EventHandler? ProcessExited;

  public bool IsDisposed { get; private set; }

  public TerminalSessionKind SessionKind { get; private set; } = TerminalSessionKind.PowerShell;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  public Task<Result> WriteInput(string input, CancellationToken cancellationToken)
  {
    // Check basic conditions before proceeding
    if (_powerShell == null || _runspace?.RunspaceStateInfo.State != RunspaceState.Opened)
    {
      return Task.FromResult(Result.Fail("PowerShell session is not running."));
    }

    // Check if we're waiting for input from a Read-Host or similar
    if (_pendingInputRequest != null)
    {
      _pendingInputRequest.SetResult(input);
      _pendingInputRequest = null;
      return Task.FromResult(Result.Ok());
    }

    // Start the PowerShell execution asynchronously and return OK immediately
    ExecutePowerShellCommandAsync(input, cancellationToken).Forget();

    return Result.Ok().AsTaskResult();
  }

  private async Task ExecutePowerShellCommandAsync(string input, CancellationToken cancellationToken)
  {
    await _writeLock.WaitAsync();

    try
    {
      if (_powerShell == null || _runspace?.RunspaceStateInfo.State != RunspaceState.Opened)
      {
        await SendOutput("PowerShell session is not running.", TerminalOutputKind.StandardError);
        return;
      }

      if (!string.IsNullOrWhiteSpace(input))
      {
        // Clear any previous commands and add the new input
        _powerShell.Commands.Clear();
        _powerShell.AddScript(input);

        // Execute the command asynchronously
        var results = await _powerShell.InvokeAsync().WaitAsync(cancellationToken);

        // Handle any errors
        if (_powerShell.HadErrors)
        {
          foreach (var error in _powerShell.Streams.Error)
          {
            await SendOutput(error.ToString(), TerminalOutputKind.StandardError);
          }
          // Clear errors after processing to prevent accumulation
          _powerShell.Streams.Error.Clear();
        }

        // Send results to output
        foreach (var result in results)
        {
          await SendOutput(result?.ToString() ?? string.Empty, TerminalOutputKind.StandardOutput);
        }
      }

      // Always send a new prompt after command execution
      await SendPrompt();
    }
    catch (OperationCanceledException)
    {
      _logger.LogWarning("PowerShell command execution timed out.");
      await SendOutput("Command execution timed out.", TerminalOutputKind.StandardError);
      await SendPrompt();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while executing PowerShell command: {Input}", input);
      await SendOutput($"Error: {ex.Message}", TerminalOutputKind.StandardError);
      await SendPrompt();
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
      
      // Platform-specific shell guidance
      var shellGuidance = _environment.Platform switch
      {
        SystemPlatform.Linux => "Type 'bash' to start bash, or use PowerShell commands.",
        SystemPlatform.MacOs => "Type 'zsh' to start zsh, or use PowerShell commands.",
        _ => "Use PowerShell commands or launch other shells."
      };
      await SendOutput(shellGuidance, TerminalOutputKind.StandardOutput);
      
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
      var prompt = $"PS {location}> ";
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
    _pendingInputRequest = new TaskCompletionSource<string>();
    
    // Wait for the next SignalR message (user input) with a reasonable timeout
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    
    try
    {
      var result = await _pendingInputRequest.Task.WaitAsync(cts.Token);
      return result;
    }
    catch (OperationCanceledException)
    {
      _pendingInputRequest?.TrySetCanceled();
      _pendingInputRequest = null;
      return string.Empty;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while waiting for host input.");
      _pendingInputRequest?.TrySetException(ex);
      _pendingInputRequest = null;
      return string.Empty;
    }
  }

  public void TriggerProcessExited()
  {
    ProcessExited?.Invoke(this, EventArgs.Empty);
  }
}
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;

namespace ControlR.Agent.Common.Services.Terminal;

public interface ITerminalSession : IDisposable
{
  event EventHandler? ProcessExited;
  bool IsDisposed { get; }

  TerminalSessionKind SessionKind { get; }
  PwshCompletionsResponseDto GetCompletions(PwshCompletionsRequestDto requestDto);

  Task<Result> WriteInput(string input, CancellationToken cancellationToken);
}

internal class TerminalSession(
  Guid terminalId,
  string viewerConnectionId,
  TimeProvider timeProvider,
  ISystemEnvironment environment,
  IHubConnection<IAgentHub> hubConnection,
  ISystemEnvironment systemEnvironment,
  ILogger<TerminalSession> logger) : ITerminalSession
{
  private readonly ISystemEnvironment _environment = environment;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<TerminalSession> _logger = logger;
  private readonly ISystemEnvironment _systemEnvironemnt = systemEnvironment;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly string _viewerConnectionId = viewerConnectionId;
  private readonly SemaphoreSlim _writeLock = new(1, 1);
  private CommandCompletion? _lastCompletion;
  private string? _lastInputText;
  private TaskCompletionSource<string>? _pendingInputRequest;
  private PowerShell? _powerShell;
  private TerminalPSHost? _psHost;
  private Runspace? _runspace;

  public event EventHandler? ProcessExited;

  public bool IsDisposed { get; private set; }

  public TerminalSessionKind SessionKind { get; private set; } = TerminalSessionKind.PowerShell;

  public Guid TerminalId { get; } = terminalId;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  public PwshCompletionsResponseDto GetCompletions(PwshCompletionsRequestDto requestDto)
  {
    var inputText = requestDto.LastCompletionInput;
    var currentIndex = requestDto.LastCursorIndex;
    var forward = requestDto.Forward;
    var page = requestDto.Page;
    var pageSize = requestDto.PageSize;

    if (_lastCompletion is null ||
        inputText != _lastInputText)
    {
      _lastInputText = inputText;
      _lastCompletion = CommandCompletion.CompleteInput(inputText, currentIndex, [], _powerShell);
    }

    if (forward.HasValue)
    {
      // If forward is specified, we only return the next result (Tab or Shift + Tab)
      var nextResult = _lastCompletion.GetNextResult(forward.Value);
      if (nextResult is null)
      {
        return new PwshCompletionsResponseDto(
          ReplacementIndex: _lastCompletion.ReplacementIndex,
          ReplacementLength: _lastCompletion.ReplacementLength,
          CompletionMatches: [],
          HasMorePages: false,
          TotalCount: 0,
          CurrentPage: 0);
      }

      var pwshMatch = new PwshCompletionMatch(
        nextResult.CompletionText,
        nextResult.ListItemText,
        (PwshCompletionMatchType)nextResult.ResultType,
        nextResult.ToolTip);

      return new PwshCompletionsResponseDto(
        ReplacementIndex: _lastCompletion.ReplacementIndex,
        ReplacementLength: _lastCompletion.ReplacementLength,
        CompletionMatches: [pwshMatch],
        HasMorePages: false,
        TotalCount: 1,
        CurrentPage: 0);
    }

    // If no forward is specified, we return a paginated result (Ctrl + Space)
    var totalCount = _lastCompletion.CompletionMatches.Count;

    if (totalCount > PwshCompletionsResponseDto.MaxRetrievableItems)
    {
      return new PwshCompletionsResponseDto(
        ReplacementIndex: _lastCompletion.ReplacementIndex,
        ReplacementLength: _lastCompletion.ReplacementLength,
        CompletionMatches: [],
        HasMorePages: false,
        TotalCount: totalCount,
        CurrentPage: page);
    }

    var pagedMatches = _lastCompletion.CompletionMatches
      .Skip(page * pageSize)
      .Take(pageSize)
      .ToArray();

    var pwshCompletions = pagedMatches
      .Select(x => new PwshCompletionMatch(x.CompletionText,
          x.ListItemText,
          (PwshCompletionMatchType)x.ResultType,
          x.ToolTip))
      .ToArray();

    var hasMorePages = (page + 1) * pageSize < totalCount;

    var completionDto = new PwshCompletionsResponseDto(
      ReplacementIndex: _lastCompletion.ReplacementIndex,
      ReplacementLength: _lastCompletion.ReplacementLength,
      CompletionMatches: pwshCompletions,
      HasMorePages: hasMorePages,
      TotalCount: totalCount,
      CurrentPage: page);

    return completionDto;
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

      if (_systemEnvironemnt.IsWindows)
      {
        _powerShell.AddScript("Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process");
        await _powerShell.InvokeAsync();
      }

      // Send shell information to the viewer
      await SendOutput($"PowerShell {PSVersionInfo.PSVersion} on {Environment.OSVersion}", TerminalOutputKind.StandardOutput);

      // Send initial prompt
      await SendPrompt();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error initializing PowerShell session.");
      throw;
    }
  }

  internal async Task SendOutput(string output, TerminalOutputKind outputKind)
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
  private async Task ExecutePowerShellCommandAsync(string input, CancellationToken cancellationToken)
  {
    await _writeLock.WaitAsync(cancellationToken);

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
          var errorLines = _powerShell.Streams.Error
            .Select(x => x.ToString())
            .ToArray();

          await SendOutput(errorLines, TerminalOutputKind.StandardError);

          // Clear errors after processing to prevent accumulation
          _powerShell.Streams.Error.Clear();
        }

        var outputLines = results
          .Select(x => x?.ToString() ?? string.Empty)
          .ToArray();

        using var ps = PowerShell.Create();
        ps.AddScript("$args[0] | Out-String");
        ps.AddArgument(results);
        var result = await ps.InvokeAsync();

        var hostOutput = result.Count > 0 ?
            $"{result[0].BaseObject}" :
            string.Empty;

        await SendOutput(hostOutput.Split(Environment.NewLine), TerminalOutputKind.StandardOutput);
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

  private void Runspace_StateChanged(object? sender, RunspaceStateEventArgs e)
  {
    if (e.RunspaceStateInfo.State is RunspaceState.Closed or RunspaceState.Broken)
    {
      ProcessExited?.Invoke(this, EventArgs.Empty);
    }
  }

  private async Task SendOutput(string[] outputLines, TerminalOutputKind kind)
  {
    var outputBuilder = new StringBuilder();
    var outputSize = 0;

    foreach (var outputLine in outputLines)
    {
      outputBuilder.AppendLine(outputLine);
      outputSize += Encoding.UTF8.GetByteCount(outputLine);
      // SignalR max message size is 32KB.  This gives us room for
      // other data on the DTO.
      if (outputSize > 20_000)
      {
        await SendOutput(outputBuilder.ToString(), kind);
        outputBuilder.Clear();
        outputSize = 0;
      }
    }

    if (outputSize > 0)
    {
      await SendOutput(outputBuilder.ToString(), kind);
      outputBuilder.Clear();
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
}
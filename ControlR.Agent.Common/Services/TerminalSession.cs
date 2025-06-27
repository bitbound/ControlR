using System.Diagnostics;
using System.Text;

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
  IFileSystem fileSystem,
  IProcessManager processManager,
  ISystemEnvironment _environment,
  IHubConnection<IAgentHub> hubConnection,
  ILogger<TerminalSession> logger) : ITerminalSession
{
  private readonly Process _shellProcess = new();
  private readonly SemaphoreSlim _writeLock = new(1, 1);
  private readonly string _viewerConnectionId = viewerConnectionId;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IProcessManager _processManager = processManager;
  private readonly ISystemEnvironment _environment = _environment;
  private readonly IHubConnection<IAgentHub> _hubConnection = hubConnection;
  private readonly ILogger<TerminalSession> _logger = logger;

  public Guid TerminalId { get; } = terminalId;

  public event EventHandler? ProcessExited;

  public bool IsDisposed { get; private set; }

  public TerminalSessionKind SessionKind { get; private set; }

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
      if (_shellProcess.HasExited)
      {
        throw new InvalidOperationException("Shell process is not running.");
      }

      using var cts = new CancellationTokenSource(timeout);

      // Write the actual input
      if (!string.IsNullOrEmpty(input))
      {
        await _shellProcess.StandardInput.WriteLineAsync(input);
      }

      // Send prompt command after the input to show current state
      await WritePromptCommand(cts.Token);

      return Result.Ok();
    }
    catch (OperationCanceledException ex)
    {
      _logger.LogError(ex, "Timed out while writing input to command shell.");
      return Result.Fail("Input command timed out.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while writing input to command shell.");
      return Result.Fail("An error occurred during input.");
    }
    finally
    {
      _writeLock.Release();
    }
  }

  internal async Task Initialize()
  {
    var shellProcessName = await GetShellProcessName();
    var psi = new ProcessStartInfo
    {
      FileName = shellProcessName,
      WindowStyle = ProcessWindowStyle.Hidden,
      Verb = "RunAs",
      UseShellExecute = false,
      RedirectStandardError = true,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      WorkingDirectory = _environment.StartupDirectory
    };

    if (SessionKind == TerminalSessionKind.PowerShell)
    {
      psi.EnvironmentVariables.Add("NO_COLOR", "1");
    }

    _shellProcess.StartInfo = psi;
    _shellProcess.ErrorDataReceived += ShellProcess_ErrorDataReceived;
    _shellProcess.OutputDataReceived += ShellProcess_OutputDataReceived;
    _shellProcess.Exited += ShellProcess_Exited;

    _shellProcess.Start();

    _shellProcess.BeginErrorReadLine();
    _shellProcess.BeginOutputReadLine();

    await WriteInput(string.Empty, TimeSpan.FromSeconds(5));
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!IsDisposed)
    {
      if (disposing)
      {
        _shellProcess.KillAndDispose();
      }

      IsDisposed = true;
    }
  }

  private async Task<string> GetShellProcessName()
  {
    switch (_environment.Platform)
    {
      case SystemPlatform.Windows:
        var result = await TryGetPwshPath();
        if (result.IsSuccess)
        {
          SessionKind = TerminalSessionKind.PowerShell;
          return result.Value;
        }

        SessionKind = TerminalSessionKind.WindowsPowerShell;
        return "powershell.exe";

      case SystemPlatform.Linux:
        if (_fileSystem.FileExists("/bin/bash"))
        {
          SessionKind = TerminalSessionKind.Bash;
          return "/bin/bash";
        }

        if (_fileSystem.FileExists("/bin/sh"))
        {
          SessionKind = TerminalSessionKind.Sh;
          return "/bin/sh";
        }

        throw new FileNotFoundException("No shell found.");
      case SystemPlatform.MacOs:
        {
          SessionKind = TerminalSessionKind.Zsh;
          return "/bin/zsh";
        }
      case SystemPlatform.Unknown:
      case SystemPlatform.MacCatalyst:
      case SystemPlatform.Android:
      case SystemPlatform.Ios:
      case SystemPlatform.Browser:
      default:
        throw new PlatformNotSupportedException();
    }
  }

  private async void ShellProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
  {
    try
    {
      var outputDto = new TerminalOutputDto(
        TerminalId,
        e.Data ?? string.Empty,
        TerminalOutputKind.StandardError,
        _timeProvider.GetLocalNow());

      await _hubConnection.Server.SendTerminalOutputToViewer(_viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal output.");
    }
  }

  private void ShellProcess_Exited(object? sender, EventArgs e)
  {
    ProcessExited?.Invoke(this, e);
  }

  private async void ShellProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
  {
    try
    {
      var outputDto = new TerminalOutputDto(
        TerminalId,
        e.Data ?? string.Empty,
        TerminalOutputKind.StandardOutput,
        _timeProvider.GetLocalNow());

      await _hubConnection.Server.SendTerminalOutputToViewer(_viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending terminal output.");
    }
  }

  private async Task<Result<string>> TryGetPwshPath()
  {
    try
    {
      var output = await _processManager.GetProcessOutput("where.exe", "pwsh.exe");
      if (!output.IsSuccess)
      {
        _logger.LogResult(output);
        return Result.Fail<string>("Failed to find path to pwsh.exe.");
      }

      var split = output.Value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
      if (split.Length == 0)
      {
        var result = Result.Fail<string>("Path to pwsh not found.");
        _logger.LogResult(result);
        return result;
      }

      return Result.Ok(split[0]);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while trying to get pwsh path.");
      return Result.Fail<string>("An error occurred.");
    }
  }

  private async Task WritePromptCommand(CancellationToken cancellationToken)
  {
    string promptCommand = SessionKind switch
    {
      TerminalSessionKind.Bash or TerminalSessionKind.Sh =>
        @"echo ""$(whoami)@$(cat /etc/hostname):$PWD$""",
      TerminalSessionKind.Zsh =>
        @"echo ""$(whoami)@$(hostname):$PWD %""",
      // PowerShell automatically outputs its prompt, so no manual prompt needed
      TerminalSessionKind.PowerShell or TerminalSessionKind.WindowsPowerShell => 
        string.Empty,
      _ => string.Empty
    };

    if (!string.IsNullOrEmpty(promptCommand))
    {
      await _shellProcess.StandardInput.WriteLineAsync(promptCommand);
    }
  }
}
using System.Diagnostics;
using System.Text;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Services;

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
  IFileSystem fileSystem,
  IProcessManager processManager,
  IEnvironmentHelper environment,
  ISystemTime systemTime,
  IAgentHubConnection hubConnection,
  ILogger<TerminalSession> logger) : ITerminalSession
{
  private readonly StringBuilder _inputBuilder = new();
  private readonly Process _shellProcess = new();
  private readonly SemaphoreSlim _writeLock = new(1, 1);
  public Guid TerminalId { get; } = terminalId;

  public event EventHandler? ProcessExited;

  public bool IsDisposed { get; private set; }

  public TerminalSessionKind SessionKind { get; private set; }

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
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

      _inputBuilder.Clear();
      _inputBuilder.Append(input);
      using var cts = new CancellationTokenSource(timeout);

      await _shellProcess.StandardInput.WriteLineAsync(_inputBuilder, cts.Token);
      _inputBuilder.Clear();

      if (SessionKind is TerminalSessionKind.Bash or TerminalSessionKind.Sh)
      {
        _inputBuilder.Append(@"echo ""$(whoami)@$(cat /etc/hostname):$PWD$""");
        await _shellProcess.StandardInput.WriteLineAsync(_inputBuilder, cts.Token);
        _inputBuilder.Clear();
      }

      if (!string.IsNullOrWhiteSpace(input))
      {
        await _shellProcess.StandardInput.WriteLineAsync(_inputBuilder, cts.Token);
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while writing input to command shell.");

      // Something's wrong.  Let the next command start a new session.
      Dispose();
      return Result.Fail("An error occurred.");
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
      WorkingDirectory = environment.StartupDirectory
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
    switch (environment.Platform)
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
        if (fileSystem.FileExists("/bin/bash"))
        {
          SessionKind = TerminalSessionKind.Bash;
          return "/bin/bash";
        }

        if (fileSystem.FileExists("/bin/sh"))
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
        systemTime.Now);

      await hubConnection.SendTerminalOutputToViewer(viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending terminal output.");
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
        systemTime.Now);

      await hubConnection.SendTerminalOutputToViewer(viewerConnectionId, outputDto);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending terminal output.");
    }
  }

  private async Task<Result<string>> TryGetPwshPath()
  {
    try
    {
      var output = await processManager.GetProcessOutput("where.exe", "pwsh.exe");
      if (!output.IsSuccess)
      {
        logger.LogResult(output);
        return Result.Fail<string>("Failed to find path to pwsh.exe.");
      }

      var split = output.Value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
      if (split.Length == 0)
      {
        var result = Result.Fail<string>("Path to pwsh not found.");
        logger.LogResult(result);
        return result;
      }

      return Result.Ok(split[0]);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while trying to get pwsh path.");
      return Result.Fail<string>("An error occurred.");
    }
  }
}
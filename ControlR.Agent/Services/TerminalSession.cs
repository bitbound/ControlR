using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Dtos;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace ControlR.Agent.Services;

public interface ITerminalSession : IDisposable
{
    event EventHandler? ProcessExited;

    bool IsDisposed { get; }

    TerminalSessionKind SessionKind { get; }

    Task<Result> WriteInput(string input, TimeSpan timeout);
}

internal class TerminalSession(
    Guid _terminalId,
    string _viewerConnectionId,
    IFileSystem _fileSystem,
    IProcessManager _processManager,
    IEnvironmentHelper _environment,
    ISystemTime _systemTime,
    IAgentHubConnection _hubConnection,
    ILogger<TerminalSession> _logger) : ITerminalSession
{
    private readonly StringBuilder _inputBuilder = new();
    private readonly Process _shellProcess = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposedValue;

    public event EventHandler? ProcessExited;

    public bool IsDisposed => _disposedValue;
    public TerminalSessionKind SessionKind { get; private set; }
    public Guid TerminalId { get; } = _terminalId;

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task<Result> WriteInput(string input, TimeSpan timeout)
    {
        await _writeLock.WaitAsync();

        try
        {
            if (_shellProcess.HasExited == true)
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
            _logger.LogError(ex, "Error while writing input to command shell.");

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
        var psi = new ProcessStartInfo()
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
        if (!_disposedValue)
        {
            if (disposing)
            {
                _shellProcess.KillAndDispose();
            }

            _disposedValue = true;
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
            case SystemPlatform.MacOS:
                {
                    SessionKind = TerminalSessionKind.Zsh;
                    return "/bin/zsh";
                }
            case SystemPlatform.Unknown:
            case SystemPlatform.MacCatalyst:
            case SystemPlatform.Android:
            case SystemPlatform.IOS:
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
                _systemTime.Now);

            await _hubConnection.SendTerminalOutputToViewer(_viewerConnectionId, outputDto);
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
                _systemTime.Now);

            await _hubConnection.SendTerminalOutputToViewer(_viewerConnectionId, outputDto);
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
}
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using Result = ControlR.Shared.Result;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class VncSessionLauncherWindows : IVncSessionLauncher
{
    private readonly IOptionsMonitor<AppOptions> _appOptions;
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);
    private readonly IDownloadsApi _downloadsApi;
    private readonly IEnvironmentHelper _environment;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<VncSessionLauncherWindows> _logger;
    private readonly IProcessInvoker _processes;

    public VncSessionLauncherWindows(
        IFileSystem fileSystem,
        IProcessInvoker processInvoker,
        IDownloadsApi downloadsApi,
        IEnvironmentHelper environment,
        IOptionsMonitor<AppOptions> appOptions,
        ILogger<VncSessionLauncherWindows> logger)
    {
        _fileSystem = fileSystem;
        _processes = processInvoker;
        _downloadsApi = downloadsApi;
        _environment = environment;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task<Result<VncSession>> CreateSession(Guid sessionId, string password)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            var startupDir = _environment.StartupDirectory;
            var vncDir = Path.Combine(startupDir, "winvnc");
            var vncFilePath = Path.Combine(vncDir, AppConstants.VncFileName);

            if (!_fileSystem.FileExists(vncFilePath))
            {
                var result = await DownloadVnc();
                if (!result.IsSuccess)
                {
                    return Result.Fail<VncSession>(result.Reason);
                }
            }

            StopProcesses();

            await CreatePassword(password);

            await SetIniOption("PortNumber", $"{_appOptions.CurrentValue.VncPort}");
            await SetIniOption("LoopbackOnly", $"{1}");

            if (_processes.GetCurrentProcess().SessionId == 0)
            {
                var createString = $"sc.exe create WinVNC binPath= \"\\\"{vncFilePath}\\\" -service\"";
                var startString = $"sc.exe start WinVNC";

                var psi = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {createString} & {startString}",
                    UseShellExecute = true
                };
                await _processes.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));

                var startResult = WaitHelper.WaitFor(
                    () =>
                    {
                        return _processes
                            .GetProcesses()
                            .Any(x =>
                                x.ProcessName.Equals("WinVNC", StringComparison.OrdinalIgnoreCase));
                    }, TimeSpan.FromSeconds(10));

                if (!startResult)
                {
                    return Result.Fail<VncSession>("VNC session failed to start.");
                }

                var session = new VncSession(
                    sessionId,
                    async () =>
                    {
                        StopProcesses();
                        var psi = new ProcessStartInfo()
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c sc.exe delete WinVNC",
                            UseShellExecute = true
                        };
                        await _processes.StartAndWaitForExit(psi, TimeSpan.FromSeconds(10));
                    });
                return Result.Ok(session);
            }
            else
            {
                var process = _processes.Start(vncFilePath, "-run", true);

                if (process?.HasExited != false)
                {
                    return Result.Fail<VncSession>("VNC session failed to start.");
                }

                var session = new VncSession(
                    sessionId,
                    () =>
                    {
                        process.KillAndDispose();
                        return Task.CompletedTask;
                    });

                return Result.Ok(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating VNC session.");
            return Result.Fail<VncSession>("An error occurred while VNC control.");
        }
        finally
        {
            _createSessionLock.Release();
        }
    }

    // For debugging.
    private static Result<string> GetSolutionDir(string currentDir)
    {
        var dirInfo = new DirectoryInfo(currentDir);
        if (!dirInfo.Exists)
        {
            return Result.Fail<string>("Not found.");
        }

        if (dirInfo.GetFiles().Any(x => x.Name == "ControlR.sln"))
        {
            return Result.Ok(currentDir);
        }

        if (dirInfo.Parent is not null)
        {
            return GetSolutionDir(dirInfo.Parent.FullName);
        }

        return Result.Fail<string>("Not found.");
    }

    private async Task CreatePassword(string password)
    {
        var createPasswordPath = Path.Combine(_environment.StartupDirectory, "winvnc", "createpassword.exe");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _processes.Start(createPasswordPath, password).WaitForExitAsync(cts.Token);
    }

    private async Task<Result> DownloadVnc()
    {
        try
        {
            var targetPath = Path.Combine(_environment.StartupDirectory, AppConstants.VncZipFileName);
            var result = await _downloadsApi.DownloadVncZipFile(targetPath);
            if (!result.IsSuccess)
            {
                return result;
            }

            ZipFile.ExtractToDirectory(targetPath, _environment.StartupDirectory, true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while extracting remote control archive.");
            return Result.Fail(ex);
        }
    }

    private async Task SetIniOption(string option, string value)
    {
        try
        {
            var iniPath = Path.Combine(_environment.StartupDirectory, "winvnc", "UltraVNC.ini");
            var lines = (await _fileSystem.ReadAllLinesAsync(iniPath)).ToList();

            if (lines.TryReplace(
                   $"{option}={value}",
                   x => x.StartsWith(option)))
            {
                await _fileSystem.WriteAllLines(iniPath, lines);
                return;
            }

            lines.Add($"{option}={value}");
            await _fileSystem.WriteAllLines(iniPath, lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set option in the ini file.");
        }
    }

    private void StopProcesses()
    {
        var processes = _processes
            .GetProcesses()
            .Where(x =>
                x.ProcessName.Equals("WinVNC", StringComparison.OrdinalIgnoreCase));

        foreach (var proc in processes)
        {
            try
            {
                proc.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop existing WinVNC process.");
            }
        }
    }
}
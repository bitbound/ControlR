using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using Result = ControlR.Shared.Result;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class VncSessionLauncherWindows : IVncSessionLauncher
{
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
        ILogger<VncSessionLauncherWindows> logger)
    {
        _fileSystem = fileSystem;
        _processes = processInvoker;
        _downloadsApi = downloadsApi;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Result<Process>> CreateSession(
        Guid sessionId,
        string password,
        Func<double, Task>? onDownloadProgress = null)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            var session = new VncSession(sessionId);

            var startupDir = _environment.StartupDirectory;
            var vncDir = Path.Combine(startupDir, "winvnc");
            var binaryPath = Path.Combine(vncDir, AppConstants.VncFileName);

            if (!_fileSystem.FileExists(binaryPath))
            {
                var result = await DownloadVnc(password, onDownloadProgress);
                if (!result.IsSuccess)
                {
                    return Result.Fail<Process>(result.Reason);
                }
            }

            StopProcesses();

            var createPasswordPath = Path.Combine(_environment.StartupDirectory, "winvnc", "createpassword.exe");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _processes.Start(createPasswordPath, password).WaitForExitAsync(cts.Token);

            var psi = new ProcessStartInfo()
            {
                FileName = binaryPath,
                WorkingDirectory = vncDir,
                UseShellExecute = true
            };

            var process = _processes.Start(psi);

            if (process is null)
            {
                return Result.Fail<Process>("VNC session failed to start.");
            }

            return Result.Ok(process);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating VNC session.");
            return Result.Fail<Process>("An error occurred while VNC control.");
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

    private async Task<Result> DownloadVnc(string password, Func<double, Task>? onDownloadProgress)
    {
        try
        {
            var targetPath = Path.Combine(_environment.StartupDirectory, AppConstants.VncZipFileName);
            var result = await _downloadsApi.DownloadVncZipFile(targetPath, onDownloadProgress);
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

    private void StopProcesses()
    {
        foreach (var proc in _processes.GetProcessesByName("winvnc"))
        {
            try
            {
                proc.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop existing winvnc process.");
            }
        }
    }
}
using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Services;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Shared;
using ControlR.Shared.Helpers;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Result = ControlR.Shared.Result;

namespace ControlR.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
internal class VncSessionLauncherWindows : IVncSessionLauncher
{
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);
    private readonly IDownloadsApi _downloadsApi;
    private readonly IElevationChecker _elevationChecker;
    private readonly IEnvironmentHelper _environment;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<VncSessionLauncherWindows> _logger;
    private readonly IProcessInvoker _processes;

    public VncSessionLauncherWindows(
        IFileSystem fileSystem,
        IProcessInvoker processInvoker,
        IDownloadsApi downloadsApi,
        IEnvironmentHelper environment,
        IElevationChecker elevationChecker,
        ILogger<VncSessionLauncherWindows> logger)
    {
        _fileSystem = fileSystem;
        _processes = processInvoker;
        _downloadsApi = downloadsApi;
        _environment = environment;
        _elevationChecker = elevationChecker;
        _logger = logger;
    }

    public async Task<Result<VncSession>> CreateSession(Guid sessionId, string password)
    {
        await _createSessionLock.WaitAsync();

        try
        {
            var tvnServerPath = Path.Combine(
                _environment.StartupDirectory,
                "TightVNC",
                "tvnserver.exe");

            if (!_fileSystem.FileExists(tvnServerPath))
            {
                var result = await DownloadTightVnc();
                if (!result.IsSuccess)
                {
                    return Result.Fail<VncSession>(result.Reason);
                }
            }

            SetRegKeys();

            var silentArgs = _elevationChecker.IsElevated() ?
                " -silent" :
                "";

            var service = ServiceController
                .GetServices()
                .FirstOrDefault(x => x.ServiceName == "tvnserver");

            if (service?.Status != ServiceControllerStatus.Running)
            {
                await _processes.StartAndWaitForExit(tvnServerPath, $"-reinstall{silentArgs}", true, TimeSpan.FromSeconds(5));
                await _processes.StartAndWaitForExit(tvnServerPath, $"-start{silentArgs}", true, TimeSpan.FromSeconds(5));
            }

            var startResult = WaitHelper.WaitFor(
                   () =>
                   {
                       return _processes
                           .GetProcesses()
                           .Any(x =>
                               x.ProcessName.Equals("tvnserver", StringComparison.OrdinalIgnoreCase));
                   }, TimeSpan.FromSeconds(10));

            if (!startResult)
            {
                return Result.Fail<VncSession>("VNC session failed to start.");
            }

            var session = new VncSession(
               sessionId,
               async () =>
               {
                   await _processes.StartAndWaitForExit(tvnServerPath, $"-stop{silentArgs}", true, TimeSpan.FromSeconds(5));
                   await _processes.StartAndWaitForExit(tvnServerPath, $"-remove{silentArgs}", true, TimeSpan.FromSeconds(5));
                   StopProcesses();
               });

            return Result.Ok(session);
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

    private static void SetRegKeys()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using var serverKey = hklm.CreateSubKey("SOFTWARE\\TightVNC\\Server");
        serverKey.SetValue("AllowLoopback", 1);
        serverKey.SetValue("LoopbackOnly", 1);
        serverKey.SetValue("UseVncAuthentication", 0);
        serverKey.SetValue("RemoveWallpaper", 0);
    }

    private async Task<Result> DownloadTightVnc()
    {
        try
        {
            var targetPath = Path.Combine(_environment.StartupDirectory, AppConstants.TightVncZipName);
            var result = await _downloadsApi.DownloadTightVncZip(targetPath);
            if (!result.IsSuccess)
            {
                return result;
            }

            var extractDir = Path.Combine(_environment.StartupDirectory, "TightVNC");
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(targetPath, extractDir);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while downloading and installing TightVNC.");
            return Result.Fail(ex);
        }
    }

    private void StopProcesses()
    {
        var processes = _processes
            .GetProcesses()
            .Where(x =>
                x.ProcessName.Equals("tvnserver", StringComparison.OrdinalIgnoreCase));

        foreach (var proc in processes)
        {
            try
            {
                proc.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop existing TightVNC process.");
            }
        }
    }
}
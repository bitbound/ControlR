using ControlR.Agent.Interfaces;
using ControlR.Agent.Models;
using ControlR.Devices.Common.Native.Windows;
using ControlR.Devices.Common.Services;
using ControlR.Devices.Common.Services.Interfaces;
using ControlR.Shared;
using ControlR.Shared.Extensions;
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
internal class VncSessionLauncherWindows(
    IFileSystem _fileSystem,
    IProcessInvoker _processInvoker,
    IDownloadsApi _downloadsApi,
    IEnvironmentHelper _environment,
    IElevationChecker _elevationChecker,
    ILogger<VncSessionLauncherWindows> _logger) : IVncSessionLauncher
{
    private readonly SemaphoreSlim _createSessionLock = new(1, 1);

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

            SetRegKeys(password);

            if (_elevationChecker.IsElevated())
            {
                return await RunTvnServerAsService(sessionId, tvnServerPath);
            }
            else
            {
                return RunTvnServerAsUser(sessionId, tvnServerPath);
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

    private async Task<Result<VncSession>> RunTvnServerAsService(Guid sessionId, string tvnServerPath)
    {
        using var service = ServiceController
            .GetServices()
            .FirstOrDefault(x => x.ServiceName == "tvnserver");

        if (service?.Status != ServiceControllerStatus.Running)
        {
            await _processInvoker.StartAndWaitForExit(tvnServerPath, "-reinstall -silent", true, TimeSpan.FromSeconds(5));
            await _processInvoker.StartAndWaitForExit(tvnServerPath, "-start -silent", true, TimeSpan.FromSeconds(5));
            await _processInvoker.StartAndWaitForExit("sc.exe", "config start= demand", true, TimeSpan.FromSeconds(5));
        }

        var startResult = await WaitHelper.WaitForAsync(
               () =>
               {
                   return _processInvoker
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
               try
               {
                   StopProcesses();
                   await _processInvoker.StartAndWaitForExit(tvnServerPath, "-stop -silent", true, TimeSpan.FromSeconds(5));
                   await _processInvoker.StartAndWaitForExit(tvnServerPath, "-remove -silent", true, TimeSpan.FromSeconds(5));
               }
               catch (Exception ex)
               {
                   _logger.LogError(ex, "Error during VNC session cleanup");
               }
           });

        return Result.Ok(session);
    }

    private Result<VncSession> RunTvnServerAsUser(Guid sessionId, string tvnServerPath)
    {
        var existingProcs = _processInvoker.GetProcessesByName(Path.GetFileNameWithoutExtension(tvnServerPath));
        var process = existingProcs.FirstOrDefault();

        process ??= _processInvoker.Start(tvnServerPath, "-run", true);

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

    private Result SetRegKeys(string password)
    {
        var encryptResult = TightVncInterop.EncryptVncPassword(password);

        if (!encryptResult.IsSuccess)
        {
            _logger.LogResult(encryptResult);
            return encryptResult.ToResult();
        }

        var hive = _elevationChecker.IsElevated() ?
            RegistryHive.LocalMachine :
            RegistryHive.CurrentUser;

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
        using var serverKey = baseKey.CreateSubKey("SOFTWARE\\TightVNC\\Server");
        serverKey.SetValue("AllowLoopback", 1);
        serverKey.SetValue("LoopbackOnly", 1);
        serverKey.SetValue("UseVncAuthentication", 1);
        serverKey.SetValue("RemoveWallpaper", 0);
        serverKey.SetValue("Password", encryptResult.Value, RegistryValueKind.Binary);
        return Result.Ok();
    }

    private void StopProcesses()
    {
        var processes = _processInvoker
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
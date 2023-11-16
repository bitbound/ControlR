using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using ControlR.Shared.Extensions;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services.Http;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Logging;
using IFileSystem = ControlR.Devices.Common.Services.IFileSystem;

namespace ControlR.Viewer.Services;

internal interface IUpdateManager
{
    Task<Result<bool>> CheckForUpdate();

    Task<Result> InstallCurrentVersion();
}

internal class UpdateManager(
    IVersionApi _versionApi,
    IDownloadsApi _downloadsApi,
    IFileSystem _fileSystem,
#if WINDOWS
    IProcessManager _processManager,
#endif
    IMessenger _messenger,
    ISettings _settings,
    ILogger<UpdateManager> _logger) : IUpdateManager
{
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public async Task<Result<bool>> CheckForUpdate()
    {
        try
        {
            var result = await _versionApi.GetCurrentViewerVersion();
            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
                return Result.Fail<bool>(result.Reason);
            }

            var currentVersion = Version.Parse(VersionTracking.CurrentVersion);
            if (result.Value != currentVersion)
            {
                _messenger.SendGenericMessage(GenericMessageKind.AppUpdateAvailable);
                return Result.Ok(true);
            }

            return Result.Ok(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new versions.");
            return Result.Fail<bool>("An error occurred.");
        }
    }

    public async Task<Result> InstallCurrentVersion()
    {
        if (!await _installLock.WaitAsync(0))
        {
            return Result.Fail("Update already started.");
        }

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), AppConstants.ViewerFileName);
            if (_fileSystem.FileExists(tempPath))
            {
                _fileSystem.DeleteFile(tempPath);
            }

            var downloadResult = await _downloadsApi.DownloadViewer(tempPath, _settings.ViewerDownloadUri);
            if (!downloadResult.IsSuccess)
            {
                return downloadResult;
            }

#if WINDOWS
            await _processManager.StartAndWaitForExit(
                  "powershell.exe",
                  $"-Command \"& {{Add-AppxPackage -Path {tempPath} -ForceApplicationShutdown -ForceUpdateFromAnyVersion}}\"",
                  true,
                  TimeSpan.FromMinutes(1));

            return Result.Ok();

#elif ANDROID
            if (Platform.AppContext.PackageManager is null)
            {
                return Result.Fail("PackageManager is unavailable.");
            }

            //var sessionId = Platform.AppContext.PackageManager.PackageInstaller.CreateSession(new PackageInstaller.SessionParams(PackageInstallMode.FullInstall));
            //var installerSession = Platform.AppContext.PackageManager.PackageInstaller.OpenSession(sessionId);
            //Platform.AppContext.ContentResolver.OpenInputStream(Android.Net.Uri.Parse(tempPath));
#else

            return Result.Fail("Not implemented.");
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while installing the current version.");
        }
        finally
        {
            _installLock.Release();
        }
        return Result.Fail("Installation failed.");
    }
}
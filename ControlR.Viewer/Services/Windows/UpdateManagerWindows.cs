#if WINDOWS
using Bitbound.SimpleMessenger;
using ControlR.Shared.Services.Http;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services.Interfaces;
using Microsoft.Extensions.Logging;
using ControlR.Devices.Common.Services;
using IFileSystem = ControlR.Devices.Common.Services.IFileSystem;
using ControlR.Devices.Common.Extensions;
using ControlR.Shared;
using Windows.ApplicationModel.Store;

namespace ControlR.Viewer.Services.Windows;

internal class UpdateManagerWindows(
    IVersionApi _versionApi,
    IDownloadsApi _downloadsApi,
    IFileSystem _fileSystem,
    IProcessManager _processManager,
    IMessenger _messenger,
    ISettings _settings,
    ILogger<UpdateManagerWindows> _logger) : IUpdateManager
{
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public async Task<Result<bool>> CheckForUpdate()
    {
        try
        {
            // If AppId is populated, this was installed from the Windows Store.
            // We'll let it manage updates.
            if (CurrentApp.AppId != Guid.Empty)
            {
                return Result.Ok(false);
            }

            var result = await _versionApi.GetCurrentViewerVersion();
            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
                return Result.Fail<bool>(result.Reason);
            }

            var currentVersion = Version.Parse(VersionTracking.CurrentVersion);
            if (result.Value != currentVersion)
            {
                await _messenger.SendGenericMessage(GenericMessageKind.AppUpdateAvailable);
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

            await _processManager.StartAndWaitForExit(
                  "powershell.exe",
                  $"-Command \"& {{" +
                  $"Add-AppxPackage -Path {tempPath} -ForceApplicationShutdown -ForceUpdateFromAnyVersion; " +
                  $"Start-Process -FilePath explorer.exe -ArgumentList shell:appsFolder\\8956DD24-5084-4303-BE59-0E1119CDB38C_44e6yepvw4x8a!App;}}\"",
                  true,
                  TimeSpan.FromMinutes(1));

            return Result.Ok();
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
#endif
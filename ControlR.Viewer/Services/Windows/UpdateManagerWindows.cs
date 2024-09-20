#if WINDOWS
using ControlR.Viewer.Services.Interfaces;
using IFileSystem = ControlR.Libraries.DevicesCommon.Services.IFileSystem;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Viewer.Extensions;

namespace ControlR.Viewer.Services.Windows;

internal class UpdateManagerWindows(
    IVersionApi versionApi,
    IDownloadsApi downloadsApi,
    IFileSystem fileSystem,
    IProcessManager processManager,
    ISettings settings,
    IStoreIntegration storeIntegration,
    IAppState appState,
    ILogger<UpdateManagerWindows> logger) : IUpdateManager
{
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public async Task<Result<bool>> CheckForUpdate()
    {
        try
        {
            if (appState.IsStoreBuild)
            {
                return await storeIntegration.IsUpdateAvailable();
            }

            return await CheckForSideloadedUpdate();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while checking for new versions.");
            return Result.Fail<bool>("An error occurred.");
        }
    }

    private async Task<Result<bool>> CheckForSideloadedUpdate()
    {
        var result = await versionApi.GetCurrentViewerVersion();
        if (!result.IsSuccess)
        {
            logger.LogResult(result);
            return Result.Fail<bool>(result.Reason);
        }

        var currentVersion = Version.Parse(VersionTracking.CurrentVersion);
        if (result.Value != currentVersion)
        {
            return Result.Ok(true);
        }

        return Result.Ok(false);
    }

    public async Task<Result> InstallCurrentVersion()
    {
        if (!await _installLock.WaitAsync(0))
        {
            return Result.Fail("Update already started.");
        }

        try
        {
            if (appState.IsStoreBuild)
            {
                var checkResult = await storeIntegration.IsUpdateAvailable();
                if (!checkResult.IsSuccess)
                {
                    return checkResult.ToResult();
                }

                return await storeIntegration.InstallCurrentVersion();
            }

            return await InstallCurrentVersionSelfHosted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while installing the current version.");
        }
        finally
        {
            _installLock.Release();
        }
        return Result.Fail("Installation failed.");
    }

    private async Task<Result> InstallCurrentVersionSelfHosted()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), AppConstants.ViewerFileName);
        if (fileSystem.FileExists(tempPath))
        {
            fileSystem.DeleteFile(tempPath);
        }

        var downloadResult = await downloadsApi.DownloadFile(settings.ViewerDownloadUri, tempPath);
        if (!downloadResult.IsSuccess)
        {
            return downloadResult;
        }

        await processManager.StartAndWaitForExit(
            fileName: "explorer.exe",
            arguments: tempPath,
            useShellExec: true,
            timeout: TimeSpan.FromMinutes(1));

        return Result.Ok();
    }
}
#endif
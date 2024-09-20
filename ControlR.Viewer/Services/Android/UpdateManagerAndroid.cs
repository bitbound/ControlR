#if ANDROID
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Services.Interfaces;
using MudBlazor;

namespace ControlR.Viewer.Services.Android;

internal class UpdateManagerAndroid(
  IVersionApi versionApi,
  IAppState appState,
  IStoreIntegration storeIntegration,
  IBrowser browser,
  ISettings settings,
  IMessenger messenger,
  ILogger<UpdateManagerAndroid> logger) : IUpdateManager
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

      if (!await browser.OpenAsync(settings.ViewerDownloadUri, BrowserLaunchMode.External))
      {
        await messenger.SendToast("Failed to launch download URL", Severity.Error);
      }

      return Result.Ok();
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

  private async Task<Result<bool>> CheckForSideloadedUpdate()
  {
    try
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
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while checking for new versions.");
      return Result.Fail<bool>("An error occurred.");
    }
  }
}
#endif
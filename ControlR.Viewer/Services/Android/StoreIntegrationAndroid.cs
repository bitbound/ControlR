#if ANDROID
using Android.Gms.Extensions;
using ControlR.Viewer.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install.Model;

namespace ControlR.Viewer.Services.Android;
internal class StoreIntegrationAndroid(
    ILogger<StoreIntegrationAndroid> _logger) : IStoreIntegration
{
    private const int _updateRequestCode = 89345;

    private readonly Uri _storePageUri = new("https://controlr.app");

    public Task<Uri> GetStorePageUri()
    {
        return _storePageUri.AsTaskResult();
    }

    public Task<Uri> GetStoreProtocolUri()
    {
        return _storePageUri.AsTaskResult();
    }

   
    public async Task<Result> InstallCurrentVersion()
    {
        try
        {
            var context = Platform.CurrentActivity;
            if (context is null)
            {
                return Result.Fail("CurrentActivity is null when checking for store updates.").Log(_logger);
            }

            using var updateManager = AppUpdateManagerFactory.Create(context);

            var getResult = await updateManager.GetAppUpdateInfo();
            if (getResult is not AppUpdateInfo info)
            {
                return Result.Fail("Unexpected result when installing current version.").Log(_logger);
            }

            var startSucceeded = updateManager.StartUpdateFlowForResult(
                   info,
                   context,
                   AppUpdateOptions
                       .NewBuilder(AppUpdateType.Immediate)
                       .SetAllowAssetPackDeletion(false)
                       .Build(),
                   _updateRequestCode);

            if (!startSucceeded)
            {
                return Result.Fail("Failed to start update flow.").Log(_logger);
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex, "Error while installing current version.").Log(_logger);
        }
    }

    public Task<Result<bool>> IsProLicenseActive()
    {
        return Result.Ok(false).AsTaskResult();
    }

    public async Task<Result<bool>> IsUpdateAvailable()
    {
        try
        {
            var context = Platform.CurrentActivity;
            if (context is null)
            {
                return Result.Fail<bool>("CurrentActivity is null when checking for store updates.").Log(_logger);
            }

            using var updateManager = AppUpdateManagerFactory.Create(context);
  
            var getResult = await updateManager.GetAppUpdateInfo();
            if (getResult is not AppUpdateInfo info)
            {
                return Result.Fail<bool>("Unexpected result when checking for store updates.").Log(_logger);
            }

            _logger.LogInformation("Available version code: {VersionCode}", info.AvailableVersionCode());

            var availability = info.UpdateAvailability();
            var isImmediateUpdatesAllowed = info.IsUpdateTypeAllowed(AppUpdateType.Immediate);

            if (availability == UpdateAvailability.UpdateAvailable && isImmediateUpdatesAllowed)
            {
                return Result.Ok(true);
            }

            return Result.Ok(false);
        }
        catch (InstallException ex) when (ex.StatusCode == InstallErrorCode.ErrorAppNotOwned)
        {
            return Result.Fail<bool>(ex, "Unable to check for store updates on side-loaded app.").Log(_logger);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>(ex, "Error while checking for store updates.").Log(_logger);
        }
    }
}
#endif
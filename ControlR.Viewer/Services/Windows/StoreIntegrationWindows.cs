#if WINDOWS
using Bitbound.SimpleMessenger;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services.Interfaces;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Windows.Services.Store;

namespace ControlR.Viewer.Services.Windows;
internal class StoreIntegrationWindows(
    IMessenger _messenger,
    ILauncher _launcher,
    ILogger<StoreIntegrationWindows> _logger) : IStoreIntegration
{
    private const string AddOnIdProSubscription = "9P0VDWFNRX3K";


    public Task<Uri> GetStorePageUri()
    {
        //try
        //{
        //    var store = StoreContext.GetDefault();
        //    var product = await store.GetStoreProductForCurrentAppAsync();
        //    return product.Product.LinkUri;
        //}
        //catch (Exception ex) 
        //{
        //    _logger.LogError(ex, "Error while getting store page.");
        //    return new Uri("https://www.microsoft.com/store/apps/9NS914B8GR04");
        //}
        return ViewerConstants.MicrosofStorePageUri.AsTaskResult();
    }

    public Task<Uri> GetStoreProtocolUri()
    {
        return ViewerConstants.MicrosoftStoreProtocolUri.AsTaskResult();
    }

    public async Task<Result<bool>> IsUpdateAvailable()
    {
        try
        {
            _logger.LogInformation("Checking Microsoft Store for updates.");
            var store = StoreContext.GetDefault();
            var updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
            _logger.LogInformation("Found {UpdateCount} update(s).", updates.Count);
            var updateAvailable =  updates.Count > 0;
            return Result.Ok(updateAvailable);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>(ex, "Error while checking store for update.").Log(_logger);
        }
    }

    public async Task<Result<bool>> IsProLicenseActive()
    {
        try
        {
            var store = StoreContext.GetDefault();
            var license = await store.GetAppLicenseAsync();
            var hasProLicense = license.AddOnLicenses.TryGetValue(AddOnIdProSubscription, out var proLicense) && proLicense.IsActive;
            return Result.Ok(hasProLicense);

        }
        catch (Exception ex)
        {
            return Result.Fail<bool>(ex, "Error while checking store for license.").Log(_logger);
        }
    }

    public async Task<Result> InstallCurrentVersion()
    {
        try
        {
            _logger.LogInformation("Starting update from Microsoft Store.");
            var store = StoreContext.GetDefault();
            if (!store.CanSilentlyDownloadStorePackageUpdates)
            {
                _logger.LogInformation("CanSilentlyDownloadStorePackageUpdates is false.");
                await _launcher.OpenAsync(ViewerConstants.MicrosoftStoreProtocolUri);
                return Result.Ok();
            }

            _logger.LogInformation("Attempting silent upgrade.");
            await _messenger.Send(new ToastMessage("Background update starting (this may take a bit)", Severity.Info));
            var updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
            var results = await store.TrySilentDownloadAndInstallStorePackageUpdatesAsync(updates);

            _logger.LogInformation(
                "Package update request sent to store.  " +
                "Overall State: {OverallState}. " +
                "Total Status Count: {UpdateStatusCount}. " +
                "Queue Count: {QueueCount}.",
                results.OverallState,
                results.StorePackageUpdateStatuses.Count,
                results.StoreQueueItems.Count);

            switch (results.OverallState)
            {
                case StorePackageUpdateState.Canceled:
                case StorePackageUpdateState.ErrorLowBattery:
                case StorePackageUpdateState.ErrorWiFiRecommended:
                case StorePackageUpdateState.OtherError:
                    await _messenger.Send(new ToastMessage("Store update error", Severity.Error));
                    await _launcher.OpenAsync(ViewerConstants.MicrosoftStoreProtocolUri);
                    break;
                default:
                    await _messenger.Send(new ToastMessage("Background update started", Severity.Info));
                    break;
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex, "Error while installing current version.").Log(_logger);
        }
    }
}
#endif
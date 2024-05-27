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

    public bool CanCheckForUpdates => true;

    public bool CanInstallUpdates => true;

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

    public async Task<bool> IsUpdateAvailable()
    {
        var store = StoreContext.GetDefault();
        var updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
        return updates.Any(x => x.Mandatory);
    }

    public async Task<bool> IsProLicenseActive()
    {
        var store = StoreContext.GetDefault();
        var license = await store.GetAppLicenseAsync();
        return license.AddOnLicenses.TryGetValue(AddOnIdProSubscription, out var proLicense) && proLicense.IsActive;
    }

    public async Task InstallCurrentVersion()
    {
        var store = StoreContext.GetDefault();
        if (!store.CanSilentlyDownloadStorePackageUpdates)
        {
            await _launcher.OpenAsync(ViewerConstants.MicrosoftStoreProtocolUri);
            return;
        }

        await _messenger.Send(new ToastMessage("Attempting update from store", Severity.Info));
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
                await _messenger.Send(new ToastMessage($"Request state: {results.OverallState}", Severity.Info));
                break;
        }
    }
}
#endif
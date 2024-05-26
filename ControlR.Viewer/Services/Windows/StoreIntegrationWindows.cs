#if WINDOWS
using Bitbound.SimpleMessenger;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Services.Interfaces;
using MudBlazor;
using Windows.Services.Store;

namespace ControlR.Viewer.Services.Windows;
internal class StoreIntegrationWindows(
    IMessenger _messenger,
    ILauncher _launcher) : IStoreIntegration
{
    private const string AddOnIdProSubscription = "9P0VDWFNRX3K";
    private readonly Uri _storeProtocolUri = new("ms-windows-store://pdp/?productid=9NS914B8GR04");

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
        return new Uri("https://www.microsoft.com/store/apps/9NS914B8GR04").AsTaskResult();
    }

    public Task<Uri> GetStoreProtocolUri()
    {
        return _storeProtocolUri.AsTaskResult();
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
            await _launcher.OpenAsync(_storeProtocolUri);
            return;
        }

        await _messenger.Send(new ToastMessage("Requesting update from store", Severity.Info));
        var updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
        await store.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
    }
}
#endif
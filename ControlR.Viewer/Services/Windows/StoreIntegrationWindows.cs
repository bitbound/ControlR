#if WINDOWS
using ControlR.Viewer.Services.Interfaces;
using Windows.Services.Store;

namespace ControlR.Viewer.Services.Windows;
internal class StoreIntegrationWindows : IStoreIntegration
{
    private const string AddOnIdProSubscription = "9P0VDWFNRX3K";

    public async Task<Uri> GetStorePageUri()
    {
        var store = StoreContext.GetDefault();
        var product = await store.GetStoreProductForCurrentAppAsync();
        return product.Product.LinkUri;
    }

    public async Task<bool> IsProLicenseActive()
    {

        var store = StoreContext.GetDefault();
        var license = await store.GetAppLicenseAsync();
        return license.AddOnLicenses.TryGetValue(AddOnIdProSubscription, out var proLicense) && proLicense.IsActive;
    }
}
#endif
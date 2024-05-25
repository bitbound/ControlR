#if WINDOWS
using ControlR.Viewer.Services.Interfaces;
using Windows.Services.Store;

namespace ControlR.Viewer.Services.Windows;
internal class StoreIntegrationWindows : IStoreIntegration
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
        return new Uri("https://www.microsoft.com/store/apps/9NS914B8GR04").AsTaskResult();
    }

    public Task<Uri> GetStoreProtocolUri()
    {
        return new Uri("ms-windows-store://pdp/?productid=9NS914B8GR04").AsTaskResult();
    }

    public async Task<bool> IsProLicenseActive()
    {
        var store = StoreContext.GetDefault();
        var license = await store.GetAppLicenseAsync();
        return license.AddOnLicenses.TryGetValue(AddOnIdProSubscription, out var proLicense) && proLicense.IsActive;
    }
}
#endif
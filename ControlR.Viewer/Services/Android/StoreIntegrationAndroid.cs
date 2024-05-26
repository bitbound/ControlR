#if ANDROID
using ControlR.Viewer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Viewer.Services.Android;
internal class StoreIntegrationAndroid : IStoreIntegration
{
    private readonly Uri _storePageUri = new("https://controlr.app");

    public bool CanCheckForUpdates => false;

    public bool CanInstallUpdates => false;

    public Task<Uri> GetStorePageUri()
    {
        return _storePageUri.AsTaskResult();
    }

    public Task<Uri> GetStoreProtocolUri()
    {
        return _storePageUri.AsTaskResult();
    }

    public Task InstallCurrentVersion()
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsProLicenseActive()
    {
        return false.AsTaskResult();
    }

    public Task<bool> IsUpdateAvailable()
    {
        throw new NotImplementedException();
    }
}
#endif
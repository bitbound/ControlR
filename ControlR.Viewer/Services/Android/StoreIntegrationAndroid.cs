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
    public Task<Uri> GetStorePageUri()
    {
        return new Uri("https://controlr.app").AsTaskResult();
    }

    public Task<Uri> GetStoreProtocolUri()
    {
        return new Uri("https://controlr.app").AsTaskResult();
    }

    public Task<bool> IsProLicenseActive()
    {
        return false.AsTaskResult();
    }
}
#endif
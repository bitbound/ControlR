#if ANDROID

using Android.App;
using Android.Content;
using Android.OS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Viewer.Services.AndroidX;

internal class LocalProxyAndroid : Service
{
    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }
}

#endif
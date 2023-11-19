#if ANDROID

using Android.App;
using Android.Content;
using ControlR.Viewer.Platforms.Android;

namespace ControlR.Viewer.Services.Android;

[BroadcastReceiver]
[IntentFilter([ProxyForegroundService.ActionStopProxy])]
public class StopProxyBroadcastReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == ProxyForegroundService.ActionStopProxy)
        {
        }
    }
}

#endif
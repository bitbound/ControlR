#if ANDROID

using Android.App;
using Android.Content;
using ControlR.Viewer.Platforms.Android;
using ControlR.Viewer.Services.Interfaces;
using MudBlazor;

namespace ControlR.Viewer.Services.Android;

internal class RdpLauncherAndroid() : IRdpLauncher
{
    public async Task<Result> LaunchRdp(int localPort)
    {
        await Task.Yield();

        if (MainActivity.Current.PackageManager is null)
        {
            return Result.Fail("PackageManager is unavailable.");
        }

        var launchIntent = MainActivity.Current.PackageManager.GetLaunchIntentForPackage("com.microsoft.rdc.androidx");
        if (launchIntent is null)
        {
            return Result.Fail("Microsoft RDP app not found.");
        }

        launchIntent.SetFlags(ActivityFlags.NewTask);
        launchIntent.SetData(global::Android.Net.Uri.Parse($"rdp://full%20address=s:127.0.0.1:{localPort}"));
        //launchIntent.PutExtra("username", "user");
        //launchIntent.PutExtra("password", "password");
        global::Android.App.Application.Context.StartActivity(launchIntent);
        return Result.Ok();
    }
}

#endif
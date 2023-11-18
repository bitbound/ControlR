#if ANDROID

using Android.Content;
using ControlR.Viewer.Services.Interfaces;
using MudBlazor;

namespace ControlR.Viewer.Services.AndroidX;

internal class RdpLauncherAndroid(ISnackbar _snackbar) : IRdpLauncher
{
    public async Task LaunchRdp(int localPort)
    {
        await Task.Yield();

        if (Platform.CurrentActivity?.PackageManager is null)
        {
            _snackbar.Add("CurrentActivity is unavailable", Severity.Warning);
            return;
        }

        var launchIntent = Platform.CurrentActivity.PackageManager.GetLaunchIntentForPackage("com.microsoft.rdc.androidx");
        if (launchIntent is null)
        {
            _snackbar.Add("Microsoft RDP app not found", Severity.Warning);
            return;
        }

        launchIntent.SetFlags(ActivityFlags.NewTask);
        launchIntent.SetData(global::Android.Net.Uri.Parse($"rdp://full%20address=s:127.0.0.1:{localPort}"));
        //launchIntent.PutExtra("username", "user");
        //launchIntent.PutExtra("password", "password");
        global::Android.App.Application.Context.StartActivity(launchIntent);
    }
}

#endif
#if ANDROID

using Android.App;
using Android.Content;
using ControlR.Viewer.Platforms.Android;
using ControlR.Viewer.Services.Interfaces;
using MudBlazor;

namespace ControlR.Viewer.Services.Android;

internal class RdpLauncherAndroid() : IRdpLauncher
{
    private const string RdpPackageName = "com.microsoft.rdc.androidx";
    public async Task<Result> LaunchRdp(int localPort)
    {
        await Task.Yield();

        if (MainActivity.Current.PackageManager is null)
        {
            return Result.Fail("PackageManager is unavailable.");
        }

        var launchIntent = MainActivity.Current.PackageManager.GetLaunchIntentForPackage(RdpPackageName);
        if (launchIntent is null)
        {
            var result = await MainPage.Current.DisplayAlert(
                 "Microsoft RDP Required",
                 "The Microsoft RDP app is required.  Press OK to open the Play Store and download.",
                 "OK",
                 "Cancel");

            if (result)
            {
                var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse("market://details?id=" + RdpPackageName));
                intent.SetFlags(ActivityFlags.NewTask);
                MainActivity.Current.StartActivity(intent);
            }
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
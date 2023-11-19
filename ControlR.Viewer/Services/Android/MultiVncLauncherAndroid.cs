#if ANDROID
using Android.Content;
using Android.Content.PM;
using ControlR.Shared.Helpers;
using ControlR.Viewer.Platforms.Android;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Viewer.Services.Android;
public interface IMultiVncLauncher
{
    Task<Result> LaunchMultiVnc(int localPort, string? password = null);
}
internal class MultiVncLauncherAndroid(IClipboard _clipboard) : IMultiVncLauncher
{
    private const string MultiVncPackageName = "com.coboltforge.dontmind.multivnc";

    public async Task<Result> LaunchMultiVnc(int localPort, string? password = null)
    {
        await Task.Yield();

        if (MainActivity.Current.PackageManager is null)
        {
            return Result.Fail("PackageManager is unavailable.");
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            await _clipboard.SetTextAsync(password);
        }
        
        if (!IsMultiVncInstalled(MainActivity.Current.PackageManager))
        {
            var result = await MainPage.Current.DisplayAlert(
                "MultiVNC Required",
                "The MultiVNC app is required.  Press OK to open the Play Store and download.",
                "OK",
                "Cancel");

            if (result)
            {
                var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse("market://details?id=" + MultiVncPackageName));
                intent.SetFlags(ActivityFlags.NewTask);
                MainActivity.Current.StartActivity(intent);
            }

            return Result.Fail("MultiVNC app not found.");
        }
        var vncUri = global::Android.Net.Uri.Parse($"vnc://127.0.0.1:{localPort}/C24bit/{password}/");
        var launchIntent = new Intent();
        launchIntent.SetAction(Intent.ActionView);
        launchIntent.AddCategory(Intent.CategoryDefault);
        launchIntent.SetPackage(MultiVncPackageName);
        launchIntent.SetData(vncUri);
        launchIntent.SetFlags(ActivityFlags.NewTask);
        MainActivity.Current.StartActivity(launchIntent);
        //launchIntent = new Intent(Intent.ActionView, vncUri);
        //launchIntent.SetDataAndNormalize(vncUri);
        //launchIntent.SetData(global::Android.Net.Uri.Parse($"vnc://127.0.0.1:{localPort}/C24bit/{password}/"));
        //await Launcher.OpenAsync($"vnc://127.0.0.1:{localPort}/C24bit/{password}/");
        //await Launcher.OpenAsync($"vnc://127.0.0.1:{localPort}?password={password}");
        return Result.Ok();
    }

    private static bool IsMultiVncInstalled(PackageManager packageManager)
    {
        try
        {
            packageManager.GetPackageInfo(MultiVncPackageName, PackageInfoFlags.Activities);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

#endif
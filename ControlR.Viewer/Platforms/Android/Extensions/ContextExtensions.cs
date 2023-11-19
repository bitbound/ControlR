using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace ControlR.Viewer.Platforms.Android.Extensions;

public static class ContextExtensions
{
    public static void StartForegroundServiceCompat<T>(
        this Context context,
        string? action = null,
        Bundle? args = null)
        where T : Service
    {
        var intent = new Intent(context, typeof(T));
        if (args != null)
        {
            intent.PutExtras(args);
        }
        if (!string.IsNullOrEmpty(action))
        {
            intent.SetAction(action);
        }
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            context.StartForegroundService(intent);
#pragma warning restore CA1416 // Validate platform compatibility
        }
        else
        {
            context.StartService(intent);
        }
    }

    public static async Task<bool> VerifyNotificationPermissions(this Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return true;
        }

        if (ContextCompat.CheckSelfPermission(MainActivity.Current, Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(MainActivity.Current, [Manifest.Permission.PostNotifications], 0);

            if (ContextCompat.CheckSelfPermission(MainActivity.Current, Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                await MainPage.Current.DisplayAlert(
                    "Permission Required",
                    "ControlR requires notification permissions in order to keep the proxy service " +
                    "running in the background. Press OK to open settings and enable notifications.",
                    "OK");

                context.StartActivity(new Intent(
                     global::Android.Provider.Settings.ActionAppNotificationSettings,
                     global::Android.Net.Uri.Parse("package:" + global::Android.App.Application.Context.PackageName)));

                return false;
            }
        }
        return true;
    }
}
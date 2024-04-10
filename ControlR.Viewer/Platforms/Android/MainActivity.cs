using Android.App;
using Android.Content;
using Android.Content.PM;
using ControlR.Viewer.Services;
using ControlR.Viewer.Services.Android;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace ControlR.Viewer.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", LaunchMode = LaunchMode.SingleTop, MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static MainActivity? _current;

    public MainActivity()
    {
        _current = this;
    }

    public static MainActivity Current
    {
        get
        {
            return _current ??
                throw new InvalidOperationException("MainActivity must be started before accessing this property.");
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        if (intent is null)
        {
            return;
        }

        if (intent.Action == UpdateManagerAndroid.PackageInstalledAction)
        {
            if (intent.Extras is null)
            {
                MauiApp.Current?.MainPage?.DisplayAlert(
                    "Install Failed",
                    $"Installation failed.  Intent.Extras is null.",
                    "OK")
                    .Forget();
                return;
            }

            var extras = intent.Extras;
            var status = extras.GetInt(PackageInstaller.ExtraStatus);
            var message = extras.GetString(PackageInstaller.ExtraStatusMessage);
            switch (status)
            {
                case (int)PackageInstallStatus.PendingUserAction:
                    // Ask user to confirm the installation
                    if (extras.Get(Intent.ExtraIntent) is Intent confirmIntent)
                    {
                        StartActivity(confirmIntent);
                    }
                    break;

                case (int)PackageInstallStatus.Success:
                    MauiApp.Current?.MainPage?.DisplayAlert(
                        "Install Successful",
                        "Installation completed successfully.",
                        "OK")
                        .Forget();
                    break;

                case (int)PackageInstallStatus.Failure:
                case (int)PackageInstallStatus.FailureAborted:
                case (int)PackageInstallStatus.FailureBlocked:
                case (int)PackageInstallStatus.FailureConflict:
                case (int)PackageInstallStatus.FailureIncompatible:
                case (int)PackageInstallStatus.FailureInvalid:
                case (int)PackageInstallStatus.FailureStorage:
                    MauiApp.Current?.MainPage?.DisplayAlert(
                        "Install Failed",
                        $"Installation failed.  Message: {message}",
                        "OK")
                        .Forget();
                    break;
            }
        }
    }
}
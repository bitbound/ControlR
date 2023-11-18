using Android.App;
using Android.Content;
using Android.Content.PM;
using ControlR.Shared.Extensions;
using ControlR.Viewer.Services;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace ControlR.Viewer.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", LaunchMode = LaunchMode.SingleTop, MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnNewIntent(Intent? intent)
    {
        if (intent?.Extras is null)
        {
            return;
        }

        var extras = intent.Extras;
        if (intent.Action == UpdateManager.PackageInstalledAction)
        {
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
                        .AndForget();
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
                        .AndForget();
                    break;
            }
        }
    }
}
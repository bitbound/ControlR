using Android.App;
using Android.Content;
using Android.Content.PM;
using ControlR.Libraries.Shared.Extensions;
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
}
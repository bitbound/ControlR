using Android.App;
using Android.Content;
using Android.OS;

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
}
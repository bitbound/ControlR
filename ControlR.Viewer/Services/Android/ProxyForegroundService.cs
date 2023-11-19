#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Graphics.Drawable;
using Bitbound.SimpleMessenger;
using ControlR.Viewer.Extensions;
using ControlR.Viewer.Models.Messages;
using ControlR.Viewer.Platforms.Android;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Intent = Android.Content.Intent;

namespace ControlR.Viewer.Services.Android;

internal interface IProxyLauncherAndroid
{
}

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
internal class ProxyForegroundService : Service, IProxyLauncherAndroid
{
    public const string ActionStartProxy = "com.jaredg.controlr.action.StartControlrProxy";
    public const string ActionStopProxy = "com.jaredg.controlr.action.StopControlrProxy";
    public const string LocalProxyServiceChannelName = "ControlR Proxy";
    public const int LocalProxyServiceNotificationId = 443809193;

    public ProxyForegroundService()
    {
        WeakReferenceMessenger.Default.RegisterGenericMessage(this, HandleGenericMessage);
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStartProxy)
        {
            StartProxy();
            WeakReferenceMessenger.Default.Send(new LocalProxyStatusChanged(true));
        }

        if (intent?.Action == ActionStopProxy)
        {
            StopSelf();
            WeakReferenceMessenger.Default.Send(new LocalProxyStatusChanged(false));
        }
        return StartCommandResult.Sticky;
    }

    [SupportedOSPlatform("android26.0")]
    private NotificationCompat.Action? BuildStopAction()
    {
        var intent = new Intent(MainActivity.Current, typeof(ProxyForegroundService));
        intent.SetAction(ActionStopProxy);
        var stopIntent = PendingIntent.GetForegroundService(MainActivity.Current, 0, intent, 0);
        return new NotificationCompat.Action(0, "Stop", stopIntent);
    }

    private void HandleGenericMessage(GenericMessageKind kind)
    {
        if (kind == GenericMessageKind.LocalProxyStopRequested)
        {
            StopSelf();
            WeakReferenceMessenger.Default.Send(new LocalProxyStatusChanged(false));
        }
    }

    private void StartProxy()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var context = Platform.CurrentActivity;

        if (context is null)
        {
            MainPage.Current.DisplayAlert(
                "Proxy Failure",
                "Failed to start local proxy. Context is null.",
                "OK");
            return;
        }

        var notificationManager = NotificationManager.FromContext(context);
        if (notificationManager is null)
        {
            MainPage.Current.DisplayAlert(
             "Notification Failure",
             "Failed to find NotificationManager.",
             "OK");
            return;
        }

        var notificationChannel = new NotificationChannel(LocalProxyServiceChannelName, LocalProxyServiceChannelName, NotificationImportance.High);
        notificationManager.CreateNotificationChannel(notificationChannel);

        using var stream = FileSystem.OpenAppPackageFileAsync("appicon.png").GetAwaiter().GetResult();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var iconBytes = ms.ToArray();

        var appIcon = IconCompat.CreateWithData(iconBytes, 0, iconBytes.Length);

        var notification = new NotificationCompat.Builder(context, LocalProxyServiceChannelName)
            .SetContentTitle("ControlR Proxy")
            .SetContentText("ControlR local proxy is running.")
            .SetSmallIcon(appIcon)
            //.SetContentIntent(BuildIntentToShowMainActivity())
            .SetOngoing(true)
            .AddAction(BuildStopAction())
            .Build();

        StartForeground(LocalProxyServiceNotificationId, notification);
    }
}

#endif
#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using ControlR.Viewer.Platforms.Android;
using ControlR.Viewer.Services;
using Bitbound.SimpleMessenger;
using ControlR.Shared.Services.Http;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using ControlR.Devices.Common.Extensions;
using ControlR.Shared.Services;
using ControlR.Viewer.Services.Interfaces;

namespace ControlR.Viewer.Services.Android;

internal class UpdateManagerAndroid(
    IVersionApi _versionApi,
    IHttpClientFactory _clientFactory,
    IDelayer _delayer,
    INotificationProvider _notify,
    IMessenger _messenger,
    ISettings _settings,
    ILogger<UpdateManagerAndroid> _logger) : IUpdateManager
{
    public const string PackageInstalledAction =
                     "com.example.android.apis.content.SESSION_API_PACKAGE_INSTALLED";

    private readonly SemaphoreSlim _installLock = new(1, 1);

    public async Task<Result<bool>> CheckForUpdate()
    {
        try
        {
            var result = await _versionApi.GetCurrentViewerVersion();
            if (!result.IsSuccess)
            {
                _logger.LogResult(result);
                return Result.Fail<bool>(result.Reason);
            }

            var currentVersion = Version.Parse(VersionTracking.CurrentVersion);
            if (result.Value != currentVersion)
            {
                await _messenger.SendGenericMessage(GenericMessageKind.AppUpdateAvailable);
                return Result.Ok(true);
            }

            return Result.Ok(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for new versions.");
            return Result.Fail<bool>("An error occurred.");
        }
    }

    public async Task<Result> InstallCurrentVersion()
    {
        if (!await _installLock.WaitAsync(0))
        {
            return Result.Fail("Update already started.");
        }

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                return await InstallCurrentVersionAndroid();
            }
            else
            {
                await _notify.DisplayAlert(
                    "Android 12 Required",
                    "Android 12 or higher is required for in-app updates.  " +
                    "Please download and install the update manually from the About page.",
                    "OK");
                return Result.Fail("Android 12 required.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while installing the current version.");
        }
        finally
        {
            _installLock.Release();
        }
        return Result.Fail("Installation failed.");
    }

    [SupportedOSPlatform("android31.0")]
    private async Task<Result> InstallCurrentVersionAndroid()
    {
        var context = Platform.CurrentActivity;
        if (context is null)
        {
            return Result.Fail("CurrentActivity is unavailable.");
        }

        if (context.PackageManager is null)
        {
            return Result.Fail("PackageManager is unavailable.");
        }

        if (context.ContentResolver is null)
        {
            return Result.Fail("ContentResolver is unavailable.");
        }

        var packageManager = context.PackageManager;
        if (!packageManager.CanRequestPackageInstalls())
        {
            await _notify.DisplayAlert(
                "Permission Required",
                "ControlR requires permission to install apps from external sources.  " +
                "Press OK to open settings and enable the permission.",
                "OK");

            context.StartActivity(new Intent(
                global::Android.Provider.Settings.ActionManageUnknownAppSources,
                global::Android.Net.Uri.Parse("package:" + global::Android.App.Application.Context.PackageName)));

            await _delayer.WaitForAsync(
                packageManager.CanRequestPackageInstalls,
                TimeSpan.MaxValue,
                1_000);
        }

        var packageName = context.PackageName;
        if (packageName is null)
        {
            return Result.Fail("Unable to determine package name.");
        }

        var packageInstaller = packageManager.PackageInstaller;
        var sessionParams = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
        sessionParams.SetAppPackageName(packageName);
        var sessionId = packageInstaller.CreateSession(sessionParams);
        var installerSession = packageInstaller.OpenSession(sessionId);

        var httpClient = _clientFactory.CreateClient();
        var apkStream = await httpClient.GetStreamAsync(_settings.ViewerDownloadUri);
        if (apkStream is null)
        {
            return Result.Fail("Failed to open APK file.");
        }
        var installerPackageStream = installerSession.OpenWrite(packageName, 0, -1);

        try
        {
            await apkStream.CopyToAsync(installerPackageStream);
            installerSession.Fsync(installerPackageStream);
        }
        finally
        {
            installerPackageStream.Close();
            apkStream.Close();
        }

        var intent = new Intent(context, context.Class);
        intent.SetAction(PackageInstalledAction);

        var receiver = PendingIntent.GetActivity(
            context,
            0,
            intent,
            PendingIntentFlags.Mutable);

        installerSession.Commit(receiver!.IntentSender);
        return Result.Ok();
    }
}
#endif
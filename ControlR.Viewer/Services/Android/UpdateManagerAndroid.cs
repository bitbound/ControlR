#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using Bitbound.SimpleMessenger;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services;
using ControlR.Viewer.Services.Interfaces;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.DevicesCommon.Messages;
using ControlR.Viewer.Extensions;

namespace ControlR.Viewer.Services.Android;

internal class UpdateManagerAndroid(
    IVersionApi _versionApi,
    IAppState _appState,
    IStoreIntegration _storeIntegration,
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
            var integrationResult = await _appState.GetStoreIntegrationEnabled(TimeSpan.FromSeconds(5));

            if (integrationResult is not bool integrationEnabled)
            {
                return Result.Ok(false);
            }

            if (!integrationEnabled)
            {
                return await CheckForSelfHostedUpdate();
            }

            // If store integration is enabled, we only want to show available update
            // if it exists in both the store and the ControlR backend.
            var checkResult = await _storeIntegration.IsUpdateAvailable();
            if (checkResult.IsSuccess)
            {
                return checkResult;
            }

            await _messenger.SendToast("Failed to check store for updates", MudBlazor.Severity.Error);

            return checkResult;
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
            var integrationResult = await _appState.GetStoreIntegrationEnabled(TimeSpan.FromSeconds(3));

            if (integrationResult is not bool integrationEnabled)
            {
                return Result.Fail("Store integration has not yet been checked.");
            }

            if (integrationEnabled)
            {
                var result = await _storeIntegration.InstallCurrentVersion();
                if (!result.IsSuccess)
                {
                    await _messenger.SendToast("Failed to update from store", MudBlazor.Severity.Error);
                }
                return result;
            }

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

private async Task<Result<bool>> CheckForSelfHostedUpdate()
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
        var updateManager = Xamarin.Google.Android.Play.Core.AppUpdate.AppUpdateManagerFactory.Create(context);
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
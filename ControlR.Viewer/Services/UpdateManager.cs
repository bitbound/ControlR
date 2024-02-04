#if ANDROID

using Android.App;
using Android.Content;
using Android.Content.PM;
using ControlR.Viewer.Platforms.Android;

#endif

#if WINDOWS
using ControlR.Devices.Common.Services;
using ControlR.Shared;
using IFileSystem = ControlR.Devices.Common.Services.IFileSystem;
#endif

using Bitbound.SimpleMessenger;
using ControlR.Shared.Services.Http;
using ControlR.Viewer.Models.Messages;
using Microsoft.Extensions.Logging;
using ControlR.Shared.Helpers;
using System.Runtime.Versioning;
using ControlR.Devices.Common.Extensions;

namespace ControlR.Viewer.Services;

internal interface IUpdateManager
{
    Task<Result<bool>> CheckForUpdate();

    Task<Result> InstallCurrentVersion();
}

internal class UpdateManager(
    IVersionApi _versionApi,
#if ANDROID
    IHttpClientFactory _clientFactory,
#endif
#if WINDOWS
    IDownloadsApi _downloadsApi,
    IFileSystem _fileSystem,
    IProcessManager _processManager,
#endif
    IMessenger _messenger,
    ISettings _settings,
    ILogger<UpdateManager> _logger) : IUpdateManager
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
#if WINDOWS
            return await InstallCurrentVersionWindows();

#elif ANDROID26_0_OR_GREATER
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                return await InstallCurrentVersionAndroid();
            }
#else

            return Result.Fail("Platform not supported.");
#endif
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

#if ANDROID

    [SupportedOSPlatform("android26.0")]
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
            await MainPage.Current.DisplayAlert(
                "Permission Required",
                "ControlR requires permission to install apps from external sources.  " +
                "Press OK to open settings and enable the permission.",
                "OK");

            context.StartActivity(new Intent(
                global::Android.Provider.Settings.ActionManageUnknownAppSources,
                global::Android.Net.Uri.Parse("package:" + global::Android.App.Application.Context.PackageName)));

            await WaitHelper.WaitForAsync(
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
            0);

        installerSession.Commit(receiver!.IntentSender);
        return Result.Ok();
    }

#endif

#if WINDOWS
    [SupportedOSPlatform("windows")]
    private async Task<Result> InstallCurrentVersionWindows()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), AppConstants.ViewerFileName);
        if (_fileSystem.FileExists(tempPath))
        {
            _fileSystem.DeleteFile(tempPath);
        }

        var downloadResult = await _downloadsApi.DownloadViewer(tempPath, _settings.ViewerDownloadUri);
        if (!downloadResult.IsSuccess)
        {
            return downloadResult;
        }

        await _processManager.StartAndWaitForExit(
              "powershell.exe",
              $"-Command \"& {{" +
              $"Add-AppxPackage -Path {tempPath} -ForceApplicationShutdown -ForceUpdateFromAnyVersion; " +
              $"Start-Process -FilePath explorer.exe -ArgumentList shell:appsFolder\\8956DD24-5084-4303-BE59-0E1119CDB38C_44e6yepvw4x8a!App;}}\"",
              true,
              TimeSpan.FromMinutes(1));

        return Result.Ok();
    }
#endif
}
using CommunityToolkit.Maui;
using ControlR.Viewer.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using FileSystem = Microsoft.Maui.Storage.FileSystem;
using FileSystemCore = ControlR.Libraries.DevicesCommon.Services.FileSystem;
using IFileSystemCore = ControlR.Libraries.DevicesCommon.Services.IFileSystem;
using CommunityToolkit.Maui.Storage;
using ControlR.Viewer.Services.Interfaces;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Libraries.Shared.Services.Buffers;




#if WINDOWS
using ControlR.Viewer.Services.Windows;
#elif ANDROID
using ControlR.Viewer.Services.Android;
#endif

namespace ControlR.Viewer;

public static class MauiProgram
{
    private static string LogPath => Path.Combine(FileSystem.Current.AppDataDirectory, "Logs", $"LogFile_{DateTime.Now:yyyy-MM-dd}.log");

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureEssentials(config =>
            {
                config.UseVersionTracking();
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
        });
        builder.Logging.AddDebug();
     
        builder.Logging.AddProvider(new FileLoggerProvider(
            VersionTracking.Default.CurrentVersion,
            () => LogPath,
            TimeSpan.FromDays(7)));


        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton(FileSystem.Current);
        builder.Services.AddSingleton(FilePicker.Default);
        builder.Services.AddSingleton(Browser.Default);
        builder.Services.AddSingleton(Clipboard.Default);
        builder.Services.AddSingleton(Launcher.Default);
        builder.Services.AddSingleton(VersionTracking.Default);
        builder.Services.AddSingleton(FolderPicker.Default);
        builder.Services.AddSingleton(FileSaver.Default);

        builder.Services.AddSingleton<IHttpConfigurer, HttpConfigurer>();
        builder.Services.AddSingleton<IKeyProvider, KeyProvider>();
        builder.Services.AddSingleton(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<ISettings, Settings>();
        builder.Services.AddSingleton<IAppState, AppState>();
        builder.Services.AddSingleton<IEnvironmentHelper>(EnvironmentHelper.Instance);
        builder.Services.AddSingleton<IViewerHubConnection, ViewerHubConnection>();
        builder.Services.AddSingleton<IDeviceCache, DeviceCache>();
        builder.Services.AddTransient<IJsInterop, JsInterop>();
        builder.Services.AddSingleton<IFileSystemCore, FileSystemCore>();
        builder.Services.AddSingleton<ISystemTime, SystemTime>();
        builder.Services.AddSingleton<IDeviceContentWindowStore, DeviceContentWindowStore>();
        builder.Services.AddSingleton<IProcessManager, ProcessManager>();
        builder.Services.AddSingleton<IMemoryProvider, MemoryProvider>();
        builder.Services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
        builder.Services.AddSingleton<IDelayer, Delayer>();
        builder.Services.AddSingleton<IRetryer, Retryer>();
        builder.Services.AddSingleton<INotificationProvider, NotificationProvider>();
        builder.Services.AddSingleton<ISettingsExporter, SettingsExporter>();
        builder.Services.AddSingleton<IUiThread, UiThread>();
        builder.Services.AddSingleton<IClipboardManager, ClipboardManager>();

        builder.Services.AddHttpClient<IKeyApi, KeyApi>(ConfigureHttpClient);
        builder.Services.AddHttpClient<IDownloadsApi, DownloadsApi>(ConfigureHttpClient);
        builder.Services.AddHttpClient<IVersionApi, VersionApi>(ConfigureAnonymousHttpClient);

        builder.Services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

#if WINDOWS
        builder.Services.AddSingleton<IUpdateManager, UpdateManagerWindows>();
        builder.Services.AddSingleton<IStoreIntegration, StoreIntegrationWindows>();
#elif ANDROID
        builder.Services.AddSingleton<IUpdateManager, UpdateManagerAndroid>();
        builder.Services.AddSingleton<IStoreIntegration, StoreIntegrationAndroid>();
#else
        throw new PlatformNotSupportedException();
#endif


#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        
        return builder.Build();
    }

    private static void ConfigureAnonymousHttpClient(IServiceProvider services, HttpClient client)
    {
        var settings = services.GetRequiredService<ISettings>();
        if (Uri.TryCreate(settings.ServerUri, UriKind.Absolute, out var serverUri))
        {
            client.BaseAddress = serverUri;
        }
    }
    private static void ConfigureHttpClient(IServiceProvider services, HttpClient client)
    {
        var httpConfig = services.GetRequiredService<IHttpConfigurer>();
        httpConfig.ConfigureClient(client);
    }
}
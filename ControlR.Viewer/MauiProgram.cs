using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Messaging;
using ControlR.Shared.Services;
using ControlR.Shared.Services.Http;
using ControlR.Viewer.Services;
using ControlR.Viewer.Services.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using FileSystemCore = ControlR.Devices.Common.Services.FileSystem;
using IFileSystemCore = ControlR.Devices.Common.Services.IFileSystem;

namespace ControlR.Viewer;

public static class MauiProgram
{
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
        builder.Logging.AddProvider(new FileLoggerProvider());
        // TODO: In-memory logger.

        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton(FileSystem.Current);
        builder.Services.AddSingleton(FilePicker.Default);
        builder.Services.AddSingleton(Browser.Default);
        builder.Services.AddSingleton(Clipboard.Default);

        builder.Services.AddSingleton(VersionTracking.Default);
        builder.Services.AddSingleton<IHttpConfigurer, HttpConfigurer>();
        builder.Services.AddSingleton<IEncryptionSessionFactory, EncryptionSessionFactory>();
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<ISettings, Settings>();
        builder.Services.AddSingleton<IAppState, AppState>();
        builder.Services.AddSingleton<IEnvironmentHelper>(EnvironmentHelper.Instance);
        builder.Services.AddSingleton<IViewerHubConnection, ViewerHubConnection>();
        builder.Services.AddSingleton<IDeviceCache, DeviceCache>();
        builder.Services.AddSingleton<IJsInterop, JsInterop>();
        builder.Services.AddSingleton<IFileSystemCore, FileSystemCore>();
        builder.Services.AddSingleton<ISystemTime, SystemTime>();

        builder.Services.AddHttpClient<IKeyApi, KeyApi>(ConfigureHttpClient);

        builder.Services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureHttpClient(IServiceProvider services, HttpClient client)
    {
        var httpConfig = services.GetRequiredService<IHttpConfigurer>();
        httpConfig.ConfigureClient(client);
    }
}
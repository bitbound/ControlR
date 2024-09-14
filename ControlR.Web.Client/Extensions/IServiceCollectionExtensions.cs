using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddControlrWebClient(this IServiceCollection services)
    {
        services.AddSingleton(WeakReferenceMessenger.Default);
        services.AddSingleton<ISettings, Settings>();
        services.AddSingleton<IBusyCounter, BusyCounter>();
        services.AddSingleton<IEnvironmentHelper>(EnvironmentHelper.Instance);
        services.AddSingleton<IViewerHubConnection, ViewerHubConnection>();
        services.AddSingleton<IDeviceCache, DeviceCache>();
        services.AddSingleton<ISystemTime, SystemTime>();
        services.AddSingleton<IDeviceContentWindowStore, DeviceContentWindowStore>();
        services.AddSingleton<IMemoryProvider, MemoryProvider>();
        services.AddSingleton<IDelayer, Delayer>();
        services.AddSingleton<IRetryer, Retryer>();
        services.AddSingleton<IClipboardManager, ClipboardManager>();
        services.AddTransient<IJsInterop, JsInterop>();
        
        services.AddHttpClient<IKeyApi, KeyApi>();
        services.AddHttpClient<IDownloadsApi, DownloadsApi>();
        services.AddHttpClient<IVersionApi, VersionApi>();
        
        services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
        services.AddTransient<IViewerStreamingClient, ViewerStreamingClient>();
        return services;
    }
}
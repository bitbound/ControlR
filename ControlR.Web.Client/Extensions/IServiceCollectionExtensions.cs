using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrWebClient(this IServiceCollection services)
  {
    services.AddScoped<IMessenger, WeakReferenceMessenger>();
    services.AddScoped<ISettings, Settings>();
    services.AddScoped<IBusyCounter, BusyCounter>();
    services.AddScoped<IEnvironmentHelper, EnvironmentHelper>();
    services.AddScoped<IViewerHubConnection, ViewerHubConnection>();
    services.AddScoped<IDeviceCache, DeviceCache>();
    services.AddScoped<ISystemTime, SystemTime>();
    services.AddScoped<IDeviceContentWindowStore, DeviceContentWindowStore>();
    services.AddScoped<IMemoryProvider, MemoryProvider>();
    services.AddScoped<IDelayer, Delayer>();
    services.AddScoped<IRetryer, Retryer>();
    services.AddScoped<IClipboardManager, ClipboardManager>();
    services.AddTransient<IJsInterop, JsInterop>();

    services.AddHttpClient<IDownloadsApi, DownloadsApi>();
    services.AddHttpClient<IVersionApi, VersionApi>();

    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
    services.AddTransient<IViewerStreamingClient, ViewerStreamingClient>();
    return services;
  }
}
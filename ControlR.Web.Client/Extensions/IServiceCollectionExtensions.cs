using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrWebClient(this IServiceCollection services, string baseAddress)
  {
    services.AddHttpClient<IVersionApi, VersionApi>(ConfigureHttpClient);
    services.AddHttpClient<IDeviceGroupsApi, DeviceGroupsApi>(ConfigureHttpClient);
    services.AddHttpClient<IServerSettingsApi, ServerSettingsApi>(ConfigureHttpClient);
    
    services.AddLazyDi();

    services.AddScoped<IMessenger, WeakReferenceMessenger>();
    services.AddScoped<ISettings, Settings>();
    services.AddScoped<IBusyCounter, BusyCounter>();
    services.AddScoped<ISystemEnvironment, SystemEnvironment>();
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

    services.AddStronglyTypedSignalrClient<IViewerHub, IViewerHubClient, ViewerHubClient>(ServiceLifetime.Scoped);

    return services;

    void ConfigureHttpClient(IServiceProvider services, HttpClient client)
    {
      client.BaseAddress = new Uri(baseAddress);
    }
  }
}
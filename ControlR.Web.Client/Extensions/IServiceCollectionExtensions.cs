using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Web.Client.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrWebClient(this IServiceCollection services, string baseAddress)
  {
    services.AddHttpClient<IControlrApi, ControlrApi>(ConfigureHttpClient);
    services.AddHttpClient<IDownloadsApi, DownloadsApi>();

    services.AddLazyDi();

    services.AddSingleton(TimeProvider.System);
    services.AddScoped<IMessenger, WeakReferenceMessenger>();
    services.AddScoped<ISettings, Settings>();
    services.AddScoped<IBusyCounter, BusyCounter>();
    services.AddScoped<ISystemEnvironment, SystemEnvironment>();
    services.AddScoped<IViewerHubConnection, ViewerHubConnection>();
    services.AddScoped<IDeviceContentWindowStore, DeviceContentWindowStore>();
    services.AddScoped<IMemoryProvider, MemoryProvider>();
    services.AddScoped<IDelayer, Delayer>();
    services.AddScoped<IRetryer, Retryer>();
    services.AddScoped<IClipboardManager, ClipboardManager>();
    services.AddScoped<IScreenWake, ScreenWake>();
    services.AddTransient<IJsInterop, JsInterop>();
    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
    services.AddTransient<IViewerStreamingClient, ViewerStreamingClient>();

    services.AddScoped<IDeviceStore, DeviceStore>();
    services.AddScoped<ITagStore, TagStore>();
    services.AddScoped<IUserStore, UserStore>();
    services.AddScoped<IRoleStore, RoleStore>();
    services.AddScoped<IInviteStore, InviteStore>();

    services.AddStronglyTypedSignalrClient<IViewerHub, IViewerHubClient, ViewerHubClient>(ServiceLifetime.Scoped);

    return services;

    void ConfigureHttpClient(IServiceProvider services, HttpClient client)
    {
      if (!string.IsNullOrWhiteSpace(baseAddress))
      {
        client.BaseAddress = new Uri(baseAddress);
      }
    }
  }
}
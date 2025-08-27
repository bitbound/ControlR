using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client.Extensions;
using ControlR.Web.Client.Services.DeviceAccess;
using ControlR.Web.Client.Services.DeviceAccess.Chat;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor.Services;

namespace ControlR.Web.Client.Startup;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddControlrWebClient(this IServiceCollection services)
  {
    services.AddMudServices(config =>
    {
      config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    });

    services.AddHttpClient<IDownloadsApi, DownloadsApi>();

    services.AddSingleton(TimeProvider.System);
    services.AddScoped<IMessenger, WeakReferenceMessenger>();
    services.AddScoped<IUserSettingsProvider, UserSettingsProvider>();
    services.AddScoped<ITenantSettingsProvider, TenantSettingsProvider>();
    services.AddScoped<IBusyCounter, BusyCounter>();
    services.AddScoped<ISystemEnvironment, SystemEnvironment>();
    services.AddScoped<IViewerHubConnection, ViewerHubConnection>();
    services.AddScoped<IDeviceContentWindowStore, DeviceContentWindowStore>();
    services.AddScoped<IMemoryProvider, MemoryProvider>();
    services.AddScoped<IDelayer, Delayer>();
    services.AddScoped<IRetryer, Retryer>();
    services.AddScoped<IClipboardManager, ClipboardManager>();
    services.AddScoped<IScreenWake, ScreenWake>();
    services.AddScoped<ISessionStorageAccessor, SessionStorageAccessor>();
    services.AddScoped<IDeviceState, DeviceState>();
    services.AddScoped<IRemoteControlState, RemoteControlState>();
    services.AddScoped<ITerminalState, TerminalState>();
    services.AddScoped<IChatState, ChatState>();
    services.AddScoped<IViewerStreamingClient, ViewerStreamingClient>();
    services.AddTransient<IJsInterop, JsInterop>();
    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

    services.AddScoped<IDeviceStore, DeviceStore>();
    services.AddScoped<IUserTagStore, UserTagStore>();
    services.AddScoped<IAdminTagStore, AdminTagStore>();
    services.AddScoped<IUserStore, UserStore>();
    services.AddScoped<IRoleStore, RoleStore>();
    services.AddScoped<IInviteStore, InviteStore>();

    services.AddStronglyTypedSignalrClient<IViewerHub, IViewerHubClient, ViewerHubClient>(ServiceLifetime.Scoped);

    return services;
  }
}
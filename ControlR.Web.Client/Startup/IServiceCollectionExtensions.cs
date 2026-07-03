using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Libraries.Shared.Services.Buffers;
using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor.Services;

namespace ControlR.Web.Client.Startup;

public static class ServiceCollectionExtensions
{
  internal static IServiceCollection AddControlrWebClient(this IServiceCollection services, Uri baseUrl)
  {
    services.AddMudServices(config =>
    {
      config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
      config.SnackbarConfiguration.HideTransitionDuration = 100;
      config.SnackbarConfiguration.ShowTransitionDuration = 300;
    });

    services.AddStronglyTypedSignalrClient<IViewerHub, IViewerHubClient, ViewerHubClient>(ServiceLifetime.Scoped);
    services.AddControlrApiClient(options => options.BaseUrl = baseUrl);
    services.AddScoped(_ => new HttpClient { BaseAddress = baseUrl });
    services.AddHttpClient<IDownloadsApi, DownloadsApi>();

    if (OperatingSystem.IsBrowser())
    {
      services.AddScoped<IAppEnvironment, BrowserAppEnvironment>();
    }
    else
    {
      services.AddScoped<IAppEnvironment, ServerAppEnvironment>();
    }

    services.AddLazyInjection();

    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IPersistentStateAccessor, PersistentStateAccessor>();

    services.AddScoped<IThemeStateProvider, ThemeStateProvider>();
    services.AddScoped<IUserPreferencesProvider, UserPreferencesProviderClient>();
    services.AddScoped<IPublicRegistrationSettingsProvider, PublicRegistrationSettingsProviderClient>();
    services.AddScoped<IMessenger, WeakReferenceMessenger>();
    services.AddScoped<IEffectiveUserPreferences, EffectiveUserPreferences>();
    services.AddScoped<ITenantSettingsProvider, TenantSettingsProvider>();
    services.AddScoped<ISystemEnvironment, SystemEnvironment>();
    services.AddScoped<IHubConnector, HubConnector>();
    services.AddScoped<IDeviceContentWindowStore, DeviceContentWindowStore>();
    services.AddScoped<IMemoryProvider, MemoryProvider>();
    services.AddScoped<IWaiter, Waiter>();
    services.AddScoped<IRetryer, Retryer>();
    services.AddScoped<IClipboardManager, ClipboardManager>();
    services.AddScoped<IHistoryEntryParser, HistoryEntryParser>();
    services.AddScoped<IScreenWake, ScreenWake>();
    services.AddScoped<ISessionStorageAccessor, SessionStorageAccessor>();
    services.AddScoped<ILocalStorageAccessor, LocalStorageAccessor>();
    services.AddScoped<IDeviceState, DeviceState>();
    services.AddScoped<IRemoteControlState, RemoteControlState>();
    services.AddScoped<ITerminalState, TerminalState>();
    services.AddScoped<IChatState, ChatState>();
    services.AddScoped<IMetricsState, MetricsState>();
    services.AddScoped<IViewerRemoteControlStream, ViewerRemoteControlStream>();
    services.AddScoped<IStreamMetrics, StreamMetrics>();
    services.AddScoped<IDeviceStore, DeviceStore>();
    services.AddScoped<IUserTagStore, UserTagStore>();
    services.AddScoped<IAdminTagStore, AdminTagStore>();
    services.AddScoped<IUserStore, UserStore>();
    services.AddScoped<IRoleStore, RoleStore>();
    services.AddScoped<IInviteStore, InviteStore>();
    services.AddScoped<IWhatsNewNotifier, WhatsNewNotifier>();

    services.AddTransient<IJsInterop, JsInterop>();
    services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
    services.AddSingleton<IMarkdownParser, MarkdownParser>();

    return services;
  }
}
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Client;

namespace ControlR.Web.Client.Services;

public interface IHubConnector
{
  Task Connect<THub>(string relativeHubEndpoint, CancellationToken cancellationToken = default)
      where THub : class;
}

internal class HubConnector(
  NavigationManager navMan,
  IServiceProvider serviceProvider,
  IHubConnection<IViewerHub> viewerHub,
  IBusyCounter busyCounter,
  IMessenger messenger,
  ILogger<HubConnector> logger) : IHubConnector
{
  private readonly IBusyCounter _busyCounter = busyCounter;
  private readonly HashSet<object> _configuredHubs = [];
  private readonly ConcurrentDictionary<Type, SemaphoreSlim> _connectLocks = [];
  private readonly ILogger<HubConnector> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly NavigationManager _navMan = navMan;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly IHubConnection<IViewerHub> _viewerHub = viewerHub;

  public async Task Connect<THub>(string relativeHubEndpoint, CancellationToken cancellationToken = default)
    where THub : class
  {
    using var _ = _busyCounter.IncrementBusyCounter();

    var hub = _serviceProvider.GetRequiredService<IHubConnection<THub>>();

    using var connectLock = await _connectLocks
      .GetOrAdd(typeof(THub), _ => new SemaphoreSlim(1, 1))
      .AcquireLockAsync(cancellationToken);

    if (hub.IsConnected)
    {
      return;
    }

    while (!cancellationToken.IsCancellationRequested)
    {
      var result = await hub.Connect(
        hubEndpoint: new Uri(new Uri(_navMan.BaseUri), relativeHubEndpoint),
        autoRetry: true,
        configure: ConfigureHttpOptions,
        cancellationToken: cancellationToken);

      if (result)
      {
        break;
      }
    }

    if (!_configuredHubs.Contains(hub))
    {
      hub.Closed += async (ex) =>
      {
        await _messenger.Send(new HubConnectionStateChangedMessage(hub.ConnectionState));
      };
      hub.Reconnecting += async (ex) =>
      {
        await _messenger.Send(new HubConnectionStateChangedMessage(hub.ConnectionState));
      };
      hub.Reconnected += async (id) =>
      {
        await _messenger.Send(new HubConnectionStateChangedMessage(hub.ConnectionState));
      };
      hub.ConnectThrew += async (ex) =>
      {
        await _messenger.Send(new ToastMessage(ex.Message, Severity.Error));
      };
      _configuredHubs.Add(hub);
    }

    await _messenger.Send(new HubConnectionStateChangedMessage(hub.ConnectionState));
  }

  private void ConfigureHttpOptions(HttpConnectionOptions options)
  {
    if (OperatingSystem.IsBrowser())
    {
      // For Blazor WebAssembly, the browser automatically adds the Identity auth cookie.
      return;
    }

    // For server-side Blazor, pass the current user's Identity auth cookie.
    var httpContextAccessor = _serviceProvider.GetRequiredService<IHttpContextAccessor>();
    var context = httpContextAccessor.HttpContext;
    if (context.User.Identity?.IsAuthenticated == true
        && context.Request.Headers.TryGetValue("Cookie", out var cookie)
        && $"{cookie}" is { Length: > 0 } cookieString)
    {
      options.Headers.Add("Cookie", cookieString);
    }
  }
}
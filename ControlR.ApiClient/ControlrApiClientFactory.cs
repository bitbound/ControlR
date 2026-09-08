using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.ApiClient;

/// <summary>
/// <para>
///   Creates and tracks <see cref="IControlrApi"/> clients that target different ControlR servers,
///   each with its own credentials.
/// </para>
/// <para>
///   Intended for backend integrations that integrate with multiple, runtime-discovered ControlR
///   servers. Register via <c>AddControlrApiClientFactory</c> and reconcile the tracked targets
///   against the server registry using <see cref="GetOrCreateClient"/>, <see cref="GetOrCreateAuthSession"/>,
///   and <see cref="TryRemoveClient"/>.
/// </para>
/// <para>
///   This type is server-only. Do not use from Blazor WebAssembly; use <c>AddControlrApiClient</c>
///   there instead, which routes through the platform's browser HTTP handler.
/// </para>
/// </summary>
public interface IControlrApiClientFactory : IDisposable
{

  /// <summary>
  /// Gets the names of the currently tracked targets.
  /// </summary>
  /// <returns>An immutable snapshot of the tracked target names.</returns>
  /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
  IReadOnlyCollection<string> GetClientNames();

  /// <summary>
  /// Gets the interactive auth session for the named target, creating it on first call.
  /// </summary>
  /// <remarks>
  /// The session is created lazily so service-account-only consumers never pay for it. The target
  /// must already have been created via <see cref="GetOrCreateClient"/>. Do not dispose the returned
  /// session directly; the factory owns it and disposes it with the target.
  /// </remarks>
  /// <param name="name">The target name previously passed to <see cref="GetOrCreateClient"/>.</param>
  /// <returns>The session for the named target.</returns>
  /// <exception cref="InvalidOperationException">No client has been created for <paramref name="name"/> yet.</exception>
  /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
  IControlrAuthSession GetOrCreateAuthSession(string name);

  /// <summary>
  /// Gets the client for the named target, creating it on first call.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   First configuration wins. <paramref name="configureOptions"/> is applied only when the name is
  ///   new; calls for an existing name ignore it entirely, including credential changes. To rotate
  ///   credentials, call <see cref="TryRemoveClient"/> and then <see cref="GetOrCreateClient"/> again
  ///   with the new options.
  /// </para>
  /// <para>
  ///   The same cached <see cref="IControlrApi"/> instance is returned for a name across calls.
  ///   Re-fetch the client per operation (or per short-lived scope) rather than caching it
  ///   indefinitely, since removal or eviction disposes the client's underlying HTTP stack.
  /// </para>
  /// </remarks>
  /// <param name="name">A consumer-chosen name identifying the target (e.g. a tenant id or server host).</param>
  /// <param name="configureOptions">Configures the target's <see cref="ControlrApiClientOptions"/>. Applied only at creation.</param>
  /// <returns>The client targeting the named server.</returns>
  /// <exception cref="OptionsValidationException">The configured options are invalid (e.g. <see cref="ControlrApiClientOptions.BaseUrl"/> is missing).</exception>
  /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
  IControlrApi GetOrCreateClient(string name, Action<ControlrApiClientOptions> configureOptions);

  /// <summary>
  /// Removes and disposes the client registered under <paramref name="name"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   Removal unlinks the target immediately and begins teardown. Sockets held by in-flight
  ///   requests are released when those requests complete. Callers still holding a reference to the
  ///   removed <see cref="IControlrApi"/> will see <see cref="ObjectDisposedException"/> on next use.
  /// </para>
  /// <para>
  ///   Whatever is currently linked under the name is removed, regardless of when it was created.
  ///   The safe credential-rotation sequence is <c>TryRemoveClient(name)</c> followed by
  ///   <c>GetOrCreateClient(name, newOptions)</c>.
  /// </para>
  /// </remarks>
  /// <param name="name">The target name to remove.</param>
  /// <returns><see langword="true"/> when a target was removed; <see langword="false"/> when no target with that name exists.</returns>
  /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
  bool TryRemoveClient(string name);
}


/// <summary>
/// The default <see cref="IControlrApiClientFactory"/> implementation. Builds a plain, self-contained
/// object graph per target (auth state, typed <see cref="HttpClient"/>, refresher, and lazily the
/// interactive auth session) without any runtime DI container.
/// </summary>
public sealed class ControlrApiClientFactory : IControlrApiClientFactory
{
  /// <summary>
  /// Placeholder assigned to <see cref="ControlrApiClientOptions.BaseUrl"/> (a required member) before
  /// the caller's configure action runs. If it survives, the caller never set a base URL.
  /// </summary>
  private static readonly Uri _unconfiguredBaseUrl = new("https://base-url-not-configured.invalid/");

  private readonly ConcurrentDictionary<string, ClientEntry> _clients = new(StringComparer.Ordinal);
  private readonly Lock _createLock = new();
  private readonly ControlrApiClientFactoryOptions _factoryOptions;
  private readonly ILoggerFactory _loggerFactory;
  private readonly ITimer _sweeperTimer;
  private readonly TimeProvider _timeProvider;

  private int _isDisposed;
  private long _touchOrdinal;

  /// <summary>
  /// Initializes a new instance of the <see cref="ControlrApiClientFactory"/> class.
  /// </summary>
  /// <param name="options">The factory options.</param>
  /// <param name="timeProvider">The time provider used for idle tracking and the sweeper timer.</param>
  /// <param name="loggerFactory">The logger factory used to create per-target loggers.</param>
  public ControlrApiClientFactory(
    ControlrApiClientFactoryOptions options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (options.SweeperInterval <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(
        nameof(options),
        options,
        $"The {nameof(ControlrApiClientFactoryOptions.SweeperInterval)} must be greater than zero.");
    }

    if (options.MaxIdleClientLifetime is { } lifetime && lifetime <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(
        nameof(options),
        options,
        $"The {nameof(ControlrApiClientFactoryOptions.MaxIdleClientLifetime)} must be greater than zero when set.");
    }

    _factoryOptions = options;
    _timeProvider = timeProvider;
    _loggerFactory = loggerFactory;

    // Periodic timer firing SweepIdleClients on the configured interval.
    _sweeperTimer = timeProvider.CreateTimer(
      static state => ((ControlrApiClientFactory)state!).SweepIdleClients(),
      this,
      options.SweeperInterval,
      options.SweeperInterval);
  }

  /// <inheritdoc />
  public void Dispose()
  {
    if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
    {
      return;
    }

    _sweeperTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    _sweeperTimer.Dispose();

    ClientEntry[] entries;
    using (_createLock.EnterScope())
    {
      entries = [.. _clients.Values];
      _clients.Clear();
    }

    foreach (var entry in entries)
    {
      entry.DisposeOnce();
    }
  }

  /// <inheritdoc />
  public IReadOnlyCollection<string> GetClientNames()
  {
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);
    return _clients.Keys.ToArray();
  }

  /// <inheritdoc />
  public IControlrAuthSession GetOrCreateAuthSession(string name)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

    if (!_clients.TryGetValue(name, out var entry))
    {
      throw new InvalidOperationException(
        $"No client has been created for target '{name}'. Call {nameof(GetOrCreateClient)} first.");
    }

    Touch(entry);
    return entry.AuthSession.Value;
  }

  /// <inheritdoc />
  public IControlrApi GetOrCreateClient(string name, Action<ControlrApiClientOptions> configureOptions)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(configureOptions);
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

    if (_clients.TryGetValue(name, out var existing))
    {
      Touch(existing);
      return existing.Api;
    }

    using (_createLock.EnterScope())
    {
      // Double-check: a concurrent creator may have linked the entry while we waited for the lock.
      if (_clients.TryGetValue(name, out existing))
      {
        Touch(existing);
        return existing.Api;
      }

      ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

      var entry = BuildEntry(name, configureOptions);
      Touch(entry);
      _clients[name] = entry;
      EvictToTrackLimit(excludedName: name);
      return entry.Api;
    }
  }

  /// <inheritdoc />
  public bool TryRemoveClient(string name)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

    ClientEntry? removed;
    using (_createLock.EnterScope())
    {
      _clients.TryRemove(name, out removed);
    }

    removed?.DisposeOnce();
    return removed is not null;
  }

  /// <summary>
  /// Evicts entries that have been idle longer than <see cref="ControlrApiClientFactoryOptions.MaxIdleClientLifetime"/>.
  /// </summary>
  internal void SweepIdleClients()
  {
    if (Volatile.Read(ref _isDisposed) == 1 || _factoryOptions.MaxIdleClientLifetime is not { } lifetime)
    {
      return;
    }

    var cutoff = _timeProvider.GetUtcNow().UtcTicks - lifetime.Ticks;
    ClientEntry[] victims;
    using (_createLock.EnterScope())
    {
      victims = [.. _clients.Values.Where(entry => Volatile.Read(ref entry.LastUsedTicks) < cutoff)];
      foreach (var victim in victims)
      {
        _clients.TryRemove(victim.Name, out _);
      }
    }

    foreach (var victim in victims)
    {
      victim.DisposeOnce();
    }
  }

  private static SocketsHttpHandler CreateDefaultHandler() =>
    new()
    {
      PooledConnectionLifetime = ControlrApiClientFactoryOptions.DefaultPooledConnectionLifetime
    };

  private ClientEntry BuildEntry(string name, Action<ControlrApiClientOptions> configureOptions)
  {
    var options = new ControlrApiClientOptions { BaseUrl = _unconfiguredBaseUrl };
    configureOptions(options);

    if (options.BaseUrl is null || ReferenceEquals(options.BaseUrl, _unconfiguredBaseUrl))
    {
      throw new OptionsValidationException(
        name,
        typeof(ControlrApiClientOptions),
        [$"The BaseUrl is required for target '{name}'."]);
    }

    var authState = new ControlrApiClientAuthState(options.PersonalAccessToken);
    var authHeaderHandler = new ControlrApiAuthHeaderHandler(authState)
    {
      InnerHandler = CreateHandler()
    };
    var httpClient = new HttpClient(authHeaderHandler)
    {
      BaseAddress = options.BaseUrl
    };
    var unauthenticatedHttpClient = new HttpClient(CreateHandler())
    {
      BaseAddress = options.BaseUrl
    };
    var unauthenticatedClientFactory = new SingleClientHttpClientFactory(unauthenticatedHttpClient);
    var refresher = new BearerTokenRefresher(authState, unauthenticatedClientFactory, _timeProvider);
    var api = new ControlrApi(
      httpClient,
      authState,
      refresher,
      _loggerFactory.CreateLogger<ControlrApi>(),
      new OptionsWrapper<ControlrApiClientOptions>(options));

    return new ClientEntry(
      name,
      api,
      httpClient,
      unauthenticatedHttpClient,
      authState,
      new Lazy<IControlrAuthSession>(
        () => new ControlrAuthSession(
          unauthenticatedClientFactory,
          authState,
          refresher,
          _loggerFactory.CreateLogger<ControlrAuthSession>(),
          new FrozenOptionsMonitor<ControlrApiClientOptions>(options),
          _timeProvider),
        LazyThreadSafetyMode.ExecutionAndPublication));
  }

  private HttpMessageHandler CreateHandler() =>
    _factoryOptions.HttpMessageHandlerFactory?.Invoke() ?? CreateDefaultHandler();

  /// <summary>
  /// When <see cref="ControlrApiClientFactoryOptions.MaxTrackedClients"/> is set, evicts
  /// least-recently-used entries until the tracked count fits under the cap. Must be called while
  /// holding <see cref="_createLock"/>. Never evicts <paramref name="excludedName"/>.
  /// </summary>
  private void EvictToTrackLimit(string excludedName)
  {
    if (_factoryOptions.MaxTrackedClients is not { } max || max < 1)
    {
      return;
    }

    while (_clients.Count > max)
    {
      var victim = _clients.Values
        .Where(entry => !StringComparer.Ordinal.Equals(entry.Name, excludedName))
        .OrderBy(entry => (Volatile.Read(ref entry.LastUsedTicks), Volatile.Read(ref entry.LastUsedOrdinal)))
        .FirstOrDefault();

      if (victim is null)
      {
        return;
      }

      _clients.TryRemove(victim.Name, out _);
      victim.DisposeOnce();
    }
  }

  private void Touch(ClientEntry entry)
  {
    Volatile.Write(ref entry.LastUsedTicks, _timeProvider.GetUtcNow().UtcTicks);
    Volatile.Write(ref entry.LastUsedOrdinal, Interlocked.Increment(ref _touchOrdinal));
  }

  private sealed class ClientEntry
  {
    public long LastUsedOrdinal;
    public long LastUsedTicks;

    private int _disposeState;

    public ClientEntry(
      string name,
      ControlrApi api,
      HttpClient httpClient,
      HttpClient unauthenticatedHttpClient,
      ControlrApiClientAuthState authState,
      Lazy<IControlrAuthSession> authSession)
    {
      Name = name;
      Api = api;
      HttpClient = httpClient;
      UnauthenticatedHttpClient = unauthenticatedHttpClient;
      AuthState = authState;
      AuthSession = authSession;
    }

    public ControlrApi Api { get; }
    public Lazy<IControlrAuthSession> AuthSession { get; }
    public ControlrApiClientAuthState AuthState { get; }
    public HttpClient HttpClient { get; }
    public string Name { get; }
    public HttpClient UnauthenticatedHttpClient { get; }

    /// <summary>
    /// Disposes the target's HTTP stacks, interactive session (if created), and bearer-refresh lock.
    /// Safe to call multiple times and concurrently; only the first call does work.
    /// </summary>
    public void DisposeOnce()
    {
      if (Interlocked.Exchange(ref _disposeState, 1) == 1)
      {
        return;
      }

      if (AuthSession.IsValueCreated)
      {
        AuthSession.Value.Dispose();
      }

      HttpClient.Dispose();
      UnauthenticatedHttpClient.Dispose();
      AuthState.BearerRefreshLock.Dispose();
    }
  }
}


/// <summary>
/// An <see cref="IHttpClientFactory"/> that always returns one pre-built client. The unauthenticated
/// endpoints (token refresh, interactive sign-in) already pass absolute URIs, so a single shared
/// client serves every target.
/// </summary>
internal sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
{
  public HttpClient CreateClient(string name) => client;
}


/// <summary>
/// An <see cref="IOptionsMonitor{TOptions}"/> that hands out one immutable, pre-built value.
/// Per-target options are frozen at creation (first-config-wins), so change tracking is meaningless.
/// </summary>
internal sealed class FrozenOptionsMonitor<TOptions>(TOptions options) : IOptionsMonitor<TOptions>
{
  public TOptions CurrentValue => options;

  public TOptions Get(string? name) => options;

  public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}

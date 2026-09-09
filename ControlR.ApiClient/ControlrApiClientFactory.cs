using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ControlR.ApiClient.Auth;
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
///   This type is server-only. Do not use from Blazor WebAssembly. Use <c>AddControlrApiClient</c>
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
  /// <para>
  ///   The session is created lazily so service-account-only consumers never pay for it. The target
  ///   must already have been created via <see cref="GetOrCreateClient"/>. Do not dispose the returned
  ///   session directly. The factory owns it and disposes it with the target.
  /// </para>
  /// <para>
  ///   The only way this fails on a live factory is a name that was never passed to
  ///   <see cref="GetOrCreateClient"/>, which is a caller bug rather than a state to handle. Once the
  ///   target exists the session is created and cached, and idle eviction will not take that target
  ///   while the session holds a live login. See
  ///   <see cref="ControlrApiClientFactoryOptions.MaxIdleClientLifetime"/>.
  /// </para>
  /// </remarks>
  /// <param name="name">The target name previously passed to <see cref="GetOrCreateClient"/>.</param>
  /// <returns>The session for the named target.</returns>
  /// <exception cref="InvalidOperationException">No client has been created for <paramref name="name"/> yet.</exception>
  /// <exception cref="ObjectDisposedException">
  /// The factory has been disposed, or the target was removed while its session was being created.
  /// </exception>
  IControlrAuthSession GetOrCreateAuthSession(string name);

  /// <summary>
  /// Gets the client for the named target, creating it on first call.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   First configuration wins. <paramref name="configureOptions"/> is applied only when the name is
  ///   new. Calls for an existing name ignore it entirely, including credential changes. To rotate
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
  /// Gets the target's interactive auth session when one has already been created.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   This creates nothing. It is the non-throwing probe for whether
  ///   <see cref="GetOrCreateAuthSession"/> has been called for <paramref name="name"/> before. Use it
  ///   to report or reconcile sign-in state across targets without materializing a session on each.
  /// </para>
  /// <para>
  ///   Unlike the other members, this does not refresh the target's last-used stamp. A consumer that
  ///   polls it cannot hold a target open, which is what lets idle eviction still reclaim a target
  ///   whose login has expired.
  /// </para>
  /// <para>
  ///   The returned session is owned by the factory. Do not dispose it, and expect that a later
  ///   removal of the target disposes it, leaving this reference disposed.
  /// </para>
  /// </remarks>
  /// <param name="name">The target name to look up.</param>
  /// <param name="session">
  /// The target's session when this returns <see langword="true"/>; <see langword="null"/> otherwise.
  /// </param>
  /// <returns><see langword="true"/> when a session already exists for the named target.</returns>
  /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
  bool TryGetAuthSession(string name, [NotNullWhen(true)] out IControlrAuthSession? session);

  /// <summary>
  /// Removes and disposes the client registered under <paramref name="name"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   Removal unlinks the target immediately. A caller still holding a reference to the removed
  ///   <see cref="IControlrApi"/> keeps getting results, but every later call fails with a reason
  ///   naming the disposed HTTP stack rather than a server fault. A streaming endpoint has no result
  ///   to fail, so it throws <see cref="ObjectDisposedException"/> carrying that same reason instead
  ///   of ending its sequence, which would read as an empty device list.
  /// </para>
  /// <para>
  ///   Calls already in flight are not cancelled. That includes a streamed response that is still
  ///   being read. The target's HTTP stack is released once the last of them finishes, which for an
  ///   <see cref="IAsyncEnumerable{T}"/> means once the caller finishes or disposes the enumeration.
  ///   Holding a slow enumeration open therefore holds the target open with it.
  /// </para>
  /// <para>
  ///   Whatever is currently linked under the name is removed, regardless of when it was created.
  ///   The safe credential-rotation sequence is <c>TryRemoveClient(name)</c> followed by
  ///   <c>GetOrCreateClient(name, newOptions)</c>.
  /// </para>
  /// </remarks>
  /// <param name="name">The target name to remove.</param>
  /// <returns><see langword="true"/> when a target was removed. <see langword="false"/> when no target with that name exists.</returns>
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

    if (options.MaxTrackedClients is { } max && max < 1)
    {
      throw new ArgumentOutOfRangeException(
        nameof(options),
        options,
        $"The {nameof(ControlrApiClientFactoryOptions.MaxTrackedClients)} must be greater than zero when set. Leave it null for no limit.");
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

    // Reading the entry's disposal state only after the session is published is what closes the
    // leak. DisposeOnce publishes disposal before it tests IsValueCreated, so whichever side starts
    // second sees the other. Without this, a session that finished constructing during teardown was
    // handed out alive, unreachable, and with a refresh loop aimed at disposed clients.
    var session = entry.AuthSession.Value;

    // The publish above happens inside Lazy, which is a release rather than a seq_cst store, so it
    // does not by itself order the read below. Without the fence this is the same store-buffering
    // outcome InFlightTracker guards against, and both sides can miss each other.
    Thread.MemoryBarrier();

    if (entry.IsDisposed)
    {
      session.Dispose();
      throw new ObjectDisposedException(
        nameof(ControlrApiClientFactory),
        $"The target '{name}' was removed while its auth session was being created.");
    }

    Touch(entry);
    return session;
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

    ClientEntry entry;
    List<ClientEntry>? evicted = null;
    using (_createLock.EnterScope())
    {
      // Double-check: a concurrent creator may have linked the entry while we waited for the lock.
      if (_clients.TryGetValue(name, out existing))
      {
        Touch(existing);
        return existing.Api;
      }

      ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

      entry = BuildEntry(name, configureOptions);
      Touch(entry);
      _clients[name] = entry;
      evicted = EvictToTrackLimit(excludedName: name);
    }

    // Dispose outside the lock so a slow handler teardown cannot stall unrelated factory calls.
    foreach (var clientEntry in evicted)
    {
      clientEntry.DisposeOnce();
    }

    return entry.Api;
  }

  /// <inheritdoc />
  public bool TryGetAuthSession(string name, [NotNullWhen(true)] out IControlrAuthSession? session)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);

    session = null;

    // Deliberately no Touch. A status page that polls this must not reset the idle clock, or it
    // would pin every target it inspects forever. Reading Value is safe because IsValueCreated was
    // checked first. Evaluating Value on an uncreated Lazy would build the session.
    if (_clients.TryGetValue(name, out var entry) && entry.AuthSession.IsValueCreated)
    {
      session = entry.AuthSession.Value;
      return true;
    }

    return false;
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
  /// Entries holding a live interactive session are never selected. See <see cref="HasLiveSession"/>.
  /// </summary>
  internal void SweepIdleClients()
  {
    if (Volatile.Read(ref _isDisposed) == 1 || _factoryOptions.MaxIdleClientLifetime is not { } lifetime)
    {
      return;
    }

    var cutoff = _timeProvider.GetUtcNow().UtcTicks - lifetime.Ticks;
    ClientEntry[] idleClients;
    using (_createLock.EnterScope())
    {
      idleClients = [.. _clients.Values.Where(entry =>
        Volatile.Read(ref entry.LastUsedTicks) < cutoff && !HasLiveSession(entry))];
      foreach (var idleClient in idleClients)
      {
        _clients.TryRemove(idleClient.Name, out _);
      }
    }

    foreach (var idleClient in idleClients)
    {
      idleClient.DisposeOnce();
    }
  }

  private static SocketsHttpHandler CreateDefaultHandler() =>
    new()
    {
      PooledConnectionLifetime = ControlrApiClientFactoryOptions.DefaultPooledConnectionLifetime
    };

  /// <summary>
  /// Whether the target holds an interactive session that a caller is still signed in to or still
  /// has to finish. Idle eviction leaves these alone because sweeping them would destroy a live
  /// login, which is state the target cannot rebuild on its own.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A live login pins its target. The session keeps renewing on its own, so the idle clock never
  /// catches up to it. Reclamation comes from the login dying instead. A sign-out, a revoked security
  /// stamp, or a rejected refresh token moves the session to
  /// <see cref="ControlrAuthSessionState.Expired"/>, which makes it sweepable again. Use
  /// <see cref="ControlrApiClientFactoryOptions.MaxTrackedClients"/> when the target count must have a
  /// bound, or <see cref="IControlrApiClientFactory.TryRemoveClient"/> to drop one on purpose.
  /// </para>
  /// <para>
  /// Reading <c>Value</c> is guarded by <see cref="Lazy{T}.IsValueCreated"/>. Evaluating it on an
  /// uncreated <see cref="Lazy{T}"/> would construct a session that nobody asked for, so the guard
  /// cannot be folded into a property pattern.
  /// </para>
  /// </remarks>
  private static bool HasLiveSession(ClientEntry entry)
  {
    if (!entry.AuthSession.IsValueCreated)
    {
      return false;
    }

    return entry.AuthSession.Value.State is ControlrAuthSessionState.Authenticated
      or ControlrAuthSessionState.AwaitingPasswordChange
      or ControlrAuthSessionState.AwaitingTwoFactor;
  }

  private static bool IsConfiguredBaseUrl([NotNullWhen(true)] Uri? baseUrl) =>
    baseUrl is not null &&
    !ReferenceEquals(baseUrl, _unconfiguredBaseUrl) &&
    baseUrl.IsAbsoluteUri &&
    baseUrl.Scheme is "http" or "https";

  private ClientEntry BuildEntry(string name, Action<ControlrApiClientOptions> configureOptions)
  {
    var options = new ControlrApiClientOptions { BaseUrl = _unconfiguredBaseUrl };
    configureOptions(options);

    if (!IsConfiguredBaseUrl(options.BaseUrl))
    {
      throw new OptionsValidationException(
        name,
        typeof(ControlrApiClientOptions),
        [$"The BaseUrl is required for target '{name}'."]);
    }

    var baseUrl = options.BaseUrl;

    // Track every allocated step so a mid-construction failure (e.g. the caller's
    // HttpMessageHandlerFactory throwing) cannot orphan sockets, handlers, or semaphores.
    HttpMessageHandler? authInnerHandler = null;
    HttpMessageHandler? unauthenticatedInnerHandler = null;
    HttpClient? httpClient = null;
    HttpClient? unauthenticatedHttpClient = null;
    var requests = new InFlightTracker();

    try
    {
      authInnerHandler = CreateHandler();
      unauthenticatedInnerHandler = CreateHandler();
      var authState = new ControlrApiClientAuthState(options.PersonalAccessToken, options.ServiceAccountApiKey);
      var authHeaderHandler = new ControlrApiAuthHeaderHandler(authState) { InnerHandler = authInnerHandler };
      httpClient = new HttpClient(authHeaderHandler) { BaseAddress = baseUrl };
      authInnerHandler = null;
      unauthenticatedHttpClient = new HttpClient(unauthenticatedInnerHandler) { BaseAddress = baseUrl };
      unauthenticatedInnerHandler = null;

      var unauthenticatedClientFactory = new SingleClientHttpClientFactory(unauthenticatedHttpClient);
      var refresher = new BearerTokenRefresher(authState, unauthenticatedClientFactory, _timeProvider)
      {
        Requests = requests
      };
      var api = new ControlrApi(
        httpClient,
        authState,
        refresher,
        _loggerFactory.CreateLogger<ControlrApi>(),
        new OptionsWrapper<ControlrApiClientOptions>(options))
      {
        Requests = requests
      };

      var entry = new ClientEntry(
        name,
        api,
        httpClient,
        unauthenticatedHttpClient,
        authState,
        requests,
        new Lazy<IControlrAuthSession>(
          () => new ControlrAuthSession(
            unauthenticatedClientFactory,
            authState,
            refresher,
            _loggerFactory.CreateLogger<ControlrAuthSession>(),
            new FrozenOptionsMonitor<ControlrApiClientOptions>(options),
            _timeProvider)
          {
            Requests = requests
          },
          LazyThreadSafetyMode.ExecutionAndPublication));

      // Wiring the release last is safe. Nothing outside BuildEntry can reach this entry, and
      // therefore no request can be counted, until the caller of GetOrCreateClient has it.
      requests.AttachRelease(entry.ReleaseHttpStack);
      return entry;
    }
    catch
    {
      // Disposing an HttpClient also disposes its handler chain, so the nulled-out references
      // above prevent double-disposal.
      httpClient?.Dispose();
      unauthenticatedHttpClient?.Dispose();
      authInnerHandler?.Dispose();
      unauthenticatedInnerHandler?.Dispose();
      throw;
    }
  }

  private HttpMessageHandler CreateHandler() =>
    _factoryOptions.HttpMessageHandlerFactory?.Invoke() ?? CreateDefaultHandler();

  /// <summary>
  /// When <see cref="ControlrApiClientFactoryOptions.MaxTrackedClients"/> is set, unlinks
  /// least-recently-used entries until the tracked count fits under the cap. Must be called while
  /// holding <see cref="_createLock"/>. Never evicts <paramref name="excludedName"/>.
  /// The caller disposes the returned entries outside the lock.
  /// </summary>
  private List<ClientEntry> EvictToTrackLimit(string excludedName)
  {
    List<ClientEntry> evicted = [];

    if (_factoryOptions.MaxTrackedClients is not { } max || max < 1)
    {
      return evicted;
    }

    while (_clients.Count > max)
    {
      var client = _clients.Values
        .Where(entry => !StringComparer.Ordinal.Equals(entry.Name, excludedName))
        .OrderBy(entry => (Volatile.Read(ref entry.LastUsedTicks), Volatile.Read(ref entry.LastUsedOrdinal)))
        .FirstOrDefault();

      if (client is null)
      {
        return evicted;
      }

      _clients.TryRemove(client.Name, out _);
      evicted.Add(client);
    }

    return evicted;
  }

  /// <summary>
  /// Stamps an entry as just used. The two writes are not atomic, and the cache-hit path in
  /// <see cref="GetOrCreateClient"/> calls this without holding <see cref="_createLock"/>, so
  /// <see cref="EvictToTrackLimit"/> can observe a new tick count paired with an older ordinal. That
  /// only perturbs which of two near-equal entries the cap picks, which an LRU heuristic tolerates,
  /// and taking the lock on the hot path to prevent it would cost more than the imprecision.
  /// </summary>
  private void Touch(ClientEntry entry)
  {
    Volatile.Write(ref entry.LastUsedTicks, _timeProvider.GetUtcNow().UtcTicks);
    Volatile.Write(ref entry.LastUsedOrdinal, Interlocked.Increment(ref _touchOrdinal));
  }

  private sealed class ClientEntry(
    string name,
    ControlrApi api,
    HttpClient httpClient,
    HttpClient unauthenticatedHttpClient,
    ControlrApiClientAuthState authState,
    InFlightTracker requests,
    Lazy<IControlrAuthSession> authSession)
  {
    public long LastUsedOrdinal;
    public long LastUsedTicks;

    private int _disposeState;

    public ControlrApi Api { get; } = api;
    public Lazy<IControlrAuthSession> AuthSession { get; } = authSession;
    public ControlrApiClientAuthState AuthState { get; } = authState;
    public HttpClient HttpClient { get; } = httpClient;
    public bool IsDisposed => Volatile.Read(ref _disposeState) == 1;
    public string Name { get; } = name;
    public InFlightTracker Requests { get; } = requests;
    public HttpClient UnauthenticatedHttpClient { get; } = unauthenticatedHttpClient;

    /// <summary>
    /// Disposes the target's HTTP stacks, interactive session (if created), and bearer-refresh lock.
    /// Safe to call multiple times and concurrently. Only the first call does work. When the target
    /// still has requests in flight, the HTTP stacks are released by the last of them instead.
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

      Requests.RequestTeardown();
    }

    public void ReleaseHttpStack()
    {
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

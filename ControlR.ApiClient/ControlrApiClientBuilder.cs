using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.ApiClient;

/// <summary>
/// <para>
///   Provides static, process-wide access to a single <see cref="IControlrApi"/> client without any
///   dependency-injection setup.
/// </para>
/// <para>
///   Internally this is a thin wrapper over <see cref="ControlrApiClientFactory"/> using the target
///   name <c>"default"</c>. Prefer the factory (or <c>AddControlrApiClient</c>) when hosting an app
///   with a service provider. Call <see cref="Initialize"/> once per process; call
///   <see cref="Dispose"/> to tear the client down and allow re-initialization.
/// </para>
/// </summary>
public static class ControlrApiClientBuilder
{
  private const string DefaultTargetName = "default";

  private static readonly Lock _servicesLock = new();

  private static IControlrApi? _client;
  private static ControlrApiClientFactory? _factory;

  /// <summary>
  /// Disposes the process-wide client created by <see cref="Initialize"/>. A subsequent call to
  /// <see cref="GetClient"/> throws <see cref="InvalidOperationException"/> until
  /// <see cref="Initialize"/> is called again.
  /// </summary>
  public static void Dispose()
  {
    ControlrApiClientFactory? factory;
    using (var lockScope = _servicesLock.EnterScope())
    {
      factory = _factory;
      _factory = null;
      _client = null;
    }

    factory?.Dispose();
  }

  /// <summary>
  ///   Gets the process-wide <see cref="IControlrApi"/> client created by <see cref="Initialize"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  ///   The client is built on a plain, self-contained object graph (no DI container). Its HTTP stack
  ///   uses a <see cref="System.Net.Sockets.SocketsHttpHandler"/> with a pooled connection lifetime,
  ///   so you don't have to worry about socket exhaustion.
  /// </para>
  /// <para>
  ///   This builder is server-only. Do not use it from Blazor WebAssembly; register the client with
  ///   <c>AddControlrApiClient</c> there instead.
  /// </para>
  /// </remarks>
  /// <exception cref="InvalidOperationException">
  ///   Thrown when <see cref="Initialize"/> has not been called (or <see cref="Dispose"/> was called since).
  /// </exception>
  public static IControlrApi GetClient()
  {
    using var lockScope = _servicesLock.EnterScope();
    if (_client is null || _factory is null)
    {
      throw new InvalidOperationException(
        $"The API client builder has not been initialized.  Call {nameof(Initialize)} first.");
    }

    // Route through the factory so the target's last-used stamp is refreshed. The configure
    // action is a no-op because first-configuration-wins ignores it for the existing name.
    return _factory.GetOrCreateClient(DefaultTargetName, static _ => { });
  }

  /// <summary>
  /// Initializes the process-wide client. Subsequent calls are ignored (first configuration wins).
  /// </summary>
  /// <param name="configureOptions">The action used to configure the <see cref="ControlrApiClientOptions"/>.</param>
  public static void Initialize(Action<ControlrApiClientOptions> configureOptions)
  {
    ArgumentNullException.ThrowIfNull(configureOptions);

    // Idle eviction is disabled because the builder is a process-wide singleton whose one
    // target must never be swept while callers keep holding the client reference.
    var factory = new ControlrApiClientFactory(
      new ControlrApiClientFactoryOptions { MaxIdleClientLifetime = null },
      TimeProvider.System,
      NullLoggerFactory.Instance);

    IControlrApi client;
    try
    {
      client = factory.GetOrCreateClient(DefaultTargetName, configureOptions);
    }
    catch
    {
      factory.Dispose();
      throw;
    }

    // Publish both fields only after successful creation, so a failed Initialize leaves the
    // builder un-initialized (GetClient keeps throwing InvalidOperationException) and a retry
    // genuinely retries instead of silently hitting the first-call-wins guard.
    using var lockScope = _servicesLock.EnterScope();
    if (_factory is not null)
    {
      factory.Dispose();
      return;
    }

    _factory = factory;
    _client = client;
  }
}

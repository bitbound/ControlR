using ControlR.ApiClient.Auth;

namespace ControlR.ApiClient;

/// <summary>
/// Options for configuring a <see cref="IControlrApiClientFactory"/>.
/// </summary>
public class ControlrApiClientFactoryOptions
{
  /// <summary>
  /// The default configuration section key for ControlR API client factory options.
  /// </summary>
  public const string SectionKey = "ControlrApiClientFactory";

  /// <summary>
  /// The pooled-connection lifetime applied to the default <see cref="SocketsHttpHandler"/> instances
  /// created by the factory for each target's authenticated and unauthenticated clients.
  /// </summary>
  internal static readonly TimeSpan DefaultPooledConnectionLifetime = TimeSpan.FromMinutes(2);

  /// <summary>
  /// Creates the primary <see cref="HttpMessageHandler"/> for each target client. When <c>null</c>,
  /// a <see cref="SocketsHttpHandler"/> with a 2-minute pooled connection lifetime is used per target.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The factory MUST return a new handler instance on every invocation. Each returned handler is
  /// owned by the target's <see cref="HttpClient"/> and is disposed when that target is evicted.
  /// Returning a shared instance causes the first eviction to break all remaining targets.
  /// </para>
  /// <para>
  /// This is intended for server-side handler customization (e.g. proxy configuration, custom
  /// TLS validation). The factory itself is server-only. See <see cref="IControlrApiClientFactory"/>.
  /// </para>
  /// </remarks>
  public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }

  /// <summary>
  /// How long a client may go unused before the background sweeper evicts and disposes it.
  /// <c>null</c> disables idle eviction. Defaults to 30 minutes.
  /// </summary>
  /// <remarks>
  /// <para>
  /// "Unused" means no call into the factory. A target's background bearer-token refresh does not
  /// count as use, but a target holding a live interactive session is never swept. A session is live
  /// while it is <see cref="ControlrAuthSessionState.Authenticated"/> or is mid-flow awaiting a
  /// two-factor code or a password change. Sweeping one would destroy a login that the target cannot
  /// rebuild, so the login is left in place.
  /// </para>
  /// <para>
  /// That means a live login pins its target indefinitely, since the session keeps renewing and the
  /// idle clock never catches it. The bound is the login dying. A sign-out, a revoked security stamp,
  /// or a rejected refresh token moves it to <see cref="ControlrAuthSessionState.Expired"/> and the
  /// next sweep takes it. Set <see cref="MaxTrackedClients"/> when the target count needs a hard bound
  /// regardless, or call <see cref="IControlrApiClientFactory.TryRemoveClient"/> to drop a target on
  /// purpose.
  /// </para>
  /// <para>
  /// Keep this non-null as the backstop for servers that were deregistered without a matching
  /// <see cref="IControlrApiClientFactory.TryRemoveClient"/> call. It reclaims credential-only
  /// targets, which hold no state worth keeping, and interactive targets whose login has died.
  /// </para>
  /// </remarks>
  public TimeSpan? MaxIdleClientLifetime { get; set; } = TimeSpan.FromMinutes(30);

  /// <summary>
  /// The maximum number of tracked targets. When the limit is reached, creating a new client
  /// evicts the least-recently-used existing client. <c>null</c> (default) means unlimited.
  /// Values below <c>1</c> are rejected at startup.
  /// </summary>
  /// <remarks>
  /// Unlike <see cref="MaxIdleClientLifetime"/>, this can evict a target that holds a live interactive
  /// session, because a hard cap has to be able to evict something. Set it for a fleet that hosts
  /// sign-ins only when losing a login and re-authenticating is acceptable.
  /// </remarks>
  public int? MaxTrackedClients { get; set; }

  /// <summary>
  /// How often the background sweeper checks for idle clients. Must be greater than
  /// <see cref="TimeSpan.Zero"/>. Defaults to 1 minute. Changes require an application restart.
  /// </summary>
  public TimeSpan SweeperInterval { get; set; } = TimeSpan.FromMinutes(1);
}

using ControlR.Libraries.DataRedaction;

namespace ControlR.Web.Server.Options;

/// <summary>
/// Application configuration options for ControlR web server.
/// </summary>
public class AppOptions
{
  /// <summary>
  /// The configuration section key for AppOptions in appsettings.json.
  /// </summary>
  public const string SectionKey = "AppOptions";

  /// <summary>
  /// The maximum allowed time difference between the agent's signed timestamp and the server's
  /// current time. If the difference exceeds this value, the agent's connection request is rejected.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A value of <c>null</c> disables timestamp verification entirely. This allows agents with
  /// unbounded clock drift to connect, but also opens the door to replay attacks — signed
  /// connection requests can be captured and reused indefinitely.
  /// </para>
  /// <para>
  /// For most deployments, a tolerance of 30-60 seconds is recommended to balance between
  /// allowing for legitimate clock skew and limiting the window for replay attacks.
  /// </para>
  /// </remarks>
  public TimeSpan? AgentClockSkewTolerance { get; init; }

  /// <summary>
  /// The number of days to retain installer key usage history.
  /// Usage entries older than this are excluded from installer key history queries and cleaned up by a background service.
  /// Values less than or equal to 0 disable history expiration and retain usage history indefinitely.
  /// </summary>
  public int AgentInstallerKeyHistoryDays { get; init; } = 90;

  /// <summary>
  /// Allows devices to self-register without requiring an installer key.
  /// When enabled, agents can bootstrap themselves without manual intervention.
  /// </summary>
  public bool AllowAgentsToSelfBootstrap { get; init; }

  /// <summary>
  /// The name that appears in TOTP authenticator apps when users set up two-factor authentication.
  /// </summary>
  public string? AuthenticatorIssuerName { get; init; }

  /// <summary>
  /// Array of allowed origins for CORS.
  /// Only used when EnableCors is true.
  /// </summary>
  public string[] CorsAllowedOrigins { get; init; } = [];

  /// <summary>
  /// The default theme mode for unauthenticated users.
  /// Authenticated users have their own per-user theme preference.
  /// </summary>
  public ThemeMode DefaultThemeMode { get; init; }

  /// <summary>
  /// Disables all email sending from the application.
  /// When enabled, no emails will be sent for account confirmation, password reset, etc.
  /// </summary>
  public bool DisableEmailSending { get; init; }

  /// <summary>
  /// The Gateway IP address that must match the IP address used by the Docker gateway.
  /// This is used for proper network configuration in Docker environments.
  /// </summary>
  public string? DockerGatewayIp { get; init; }

  /// <summary>
  /// Automatically obtains Cloudflare IPs from https://www.cloudflare.com/ips-v4
  /// and adds them to the KnownNetworks list for forwarded headers.
  /// </summary>
  public bool EnableCloudflareProxySupport { get; init; }

  /// <summary>
  /// When enabled, CORS middleware will be applied and origins specified
  /// in CorsAllowedOrigins will be permitted. If disabled, CORS is not applied.
  /// </summary>
  public bool EnableCors { get; init; }

  /// <summary>
  /// Enables detailed error messages from Entity Framework Core when database errors occur.
  /// This can be helpful for debugging but may leak sensitive information, so it is disabled by default.
  /// Enabling this incurs a small performance cost.
  /// </summary>
  public bool EnableDatabaseDetailedErrors { get; init; }

  /// <summary>
  /// Whether to enable the configuration provider for Docker Secrets.
  /// When enabled, the application will read secrets from Docker's secret management system.
  /// </summary>
  public bool EnableDockerSecrets { get; init; }

  /// <summary>
  /// Controls the first-user auto-promotion behavior.
  /// When enabled, an empty server instance temporarily treats public registration as available,
  /// and the first tenantless user created by that flow is promoted to server administrator.
  /// </summary>
  public bool EnableFirstUserBootstrap { get; init; }

  /// <summary>
  /// Enables the interactive bearer-token login flow exposed through ASP.NET Core Identity API endpoints.
  /// </summary>
  public bool EnableInteractiveBearerLogin { get; init; }

  /// <summary>
  /// When enabled, bypasses KnownProxies/KnownIpNetworks checks and trusts all forwarded headers
  /// from the reverse proxy. Only enable this in secure environments where the reverse proxy
  /// is guaranteed to be the only source of incoming traffic.
  /// </summary>
  public bool EnableNetworkTrust { get; init; }

  /// <summary>
  /// Whether to make self-registration publicly available.
  /// When enabled, users can create accounts without requiring an invitation.
  /// </summary>
  public bool EnablePublicRegistration { get; init; }

  /// <summary>
  /// When enabled, the Scalar UI endpoint for exploring the OpenAPI document is served.
  /// Recommended for development/debugging; disable in production.
  /// </summary>
  public bool EnableScalarUi { get; init; }

  /// <summary>
  /// If enabled, detailed errors will be sent to the SignalR client when exceptions occur
  /// during hub method invocations. This can be helpful for debugging but may leak sensitive
  /// information, so it is disabled by default.
  /// </summary>
  public bool EnableSignalrDetailedErrors { get; init; }

  /// <summary>
  /// Number of days after which external user accounts with no recent login activity are cleaned up.
  /// External accounts whose <see cref="AppUser.LastLogin"/> is older than this
  /// threshold are removed by a background service. Accounts that have never logged in are not removed.
  /// Set to 0 or less to disable external user cleanup.
  /// </summary>
  public int ExternalUserCleanupAfterDays { get; init; } = 30;

  /// <summary>
  /// The client ID for GitHub OAuth authentication.
  /// Create an OAuth app in GitHub and set this value to enable GitHub login.
  /// </summary>
  public string? GitHubClientId { get; init; }

  /// <summary>
  /// The client secret for GitHub OAuth authentication.
  /// This value is protected and should be stored securely.
  /// </summary>
  [ProtectedDataClassification]
  public string? GitHubClientSecret { get; init; }

  /// <summary>
  /// The name of the in-memory database to use when UseInMemoryDatabase is enabled.
  /// Primarily used for testing and development environments.
  /// </summary>
  public string? InMemoryDatabaseName { get; init; }

  /// <summary>
  /// Lifetime of interactive bearer access tokens issued by ASP.NET Core Identity.
  /// </summary>
  public int InteractiveBearerTokenExpirationMinutes { get; init; } = 60;

  /// <summary>
  /// Lifetime of refresh tokens issued by ASP.NET Core Identity for interactive bearer logins.
  /// </summary>
  public int InteractiveRefreshTokenExpirationDays { get; init; } = 30;

  /// <summary>
  /// Array of known network CIDR ranges that are trusted for forwarded headers.
  /// Used by the ForwardedHeadersMiddleware to validate proxy requests.
  /// </summary>
  public string[] KnownNetworks { get; init; } = [];

  /// <summary>
  /// Array of known proxy IP addresses that are trusted for forwarded headers.
  /// Used by the ForwardedHeadersMiddleware to validate proxy requests.
  /// </summary>
  public string[] KnownProxies { get; init; } = [];

  /// <summary>
  /// The maximum allowed file size for transfers in the remote File System component.
  /// Set to 0 or less for no limit. Default is 100 MB (104857600 bytes).
  /// </summary>
  public long MaxFileTransferSize { get; init; } = 100 * 1024 * 1024;// 100 MB default

  /// <summary>
  /// The client ID for Microsoft account authentication.
  /// Create an App Registration in Azure and set this value to enable Microsoft login.
  /// </summary>
  public string? MicrosoftClientId { get; init; }

  /// <summary>
  /// The client secret for Microsoft account authentication.
  /// This value is protected and should be stored securely.
  /// </summary>
  [ProtectedDataClassification]
  public string? MicrosoftClientSecret { get; init; }

  /// <summary>
  /// If enabled, signing in with a passkey will effectively add the "remember me" option.
  /// This provides a more seamless authentication experience for passkey users.
  /// </summary>
  public bool PersistPasskeyLogin { get; init; }

  /// <summary>
  /// Whether users must confirm their email address before being allowed to log in.
  /// If true, you must also configure SMTP settings below.
  /// </summary>
  public bool RequireUserEmailConfirmation { get; init; }

  /// <summary>
  /// Whether each user must have a unique email address.
  /// When set to false, multiple accounts can share the same email, or they can have no email at all.
  /// Defaults to true in production, false in development.
  /// </summary>
  public bool RequireUserUniqueEmail { get; init; } = true;

  /// <summary>
  /// Maximum failed service-account authentication attempts allowed per limiter window.
  /// Set to 0 or less to disable the pre-authentication limiter for x-api-key requests.
  /// </summary>
  public int ServiceAccountAuthFailureLimit { get; init; } = 5;

  /// <summary>
  /// Size of the fixed window used by the service-account authentication rate limiter.
  /// Set to 0 or less to disable the pre-authentication limiter for x-api-key requests.
  /// </summary>
  public int ServiceAccountAuthFailureWindowMinutes { get; init; } = 5;

  /// <summary>
  /// Whether to check certificate revocation for SMTP connections.
  /// Enabled by default for security.
  /// </summary>
  public bool SmtpCheckCertificateRevocation { get; init; } = true;

  /// <summary>
  /// The display name used in outgoing emails from the application.
  /// Used for account confirmation and password reset emails.
  /// </summary>
  public string? SmtpDisplayName { get; init; }

  /// <summary>
  /// The email address used as the sender for outgoing emails.
  /// Used for account confirmation and password reset emails.
  /// </summary>
  public string? SmtpEmail { get; init; }

  /// <summary>
  /// The SMTP server hostname or IP address for sending emails.
  /// Used for account confirmation and password reset emails.
  /// </summary>
  public string? SmtpHost { get; init; }

  /// <summary>
  /// The local domain for SMTP connections.
  /// Optional setting for SMTP configuration.
  /// </summary>
  public string? SmtpLocalDomain { get; init; }

  /// <summary>
  /// The password for SMTP authentication.
  /// This value is protected and should be stored securely.
  /// </summary>
  [ProtectedDataClassification]
  public string? SmtpPassword { get; init; }

  /// <summary>
  /// The port number for SMTP connections.
  /// Default is 587 (submission port).
  /// </summary>
  public int SmtpPort { get; init; } = 587;

  /// <summary>
  /// The username for SMTP authentication.
  /// Used when the SMTP server requires authentication.
  /// </summary>
  public string? SmtpUserName { get; init; }

  /// <summary>
  /// When enabled, extra logs will be written for all HTTP requests and responses.
  /// This is useful for debugging but should be disabled in production for performance.
  /// See https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-logging for details.
  /// </summary>
  public bool UseHttpLogging { get; init; }

  /// <summary>
  /// When enabled, uses an in-memory database instead of PostgreSQL.
  /// Primarily used for testing and development environments.
  /// </summary>
  public bool UseInMemoryDatabase { get; init; }
}
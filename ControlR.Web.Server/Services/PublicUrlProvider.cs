using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.WebUtilities;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Resolves the absolute base URL for links that leave this server, such as the callbacks embedded in
/// account emails.
/// </summary>
/// <remarks>
/// <para>
/// An emailed link reaches its recipient on this server's authority, so its origin must never come from
/// a request an attacker controls. A forged <c>Host</c> or <c>X-Forwarded-Host</c> header otherwise
/// produces a genuine email carrying a valid token that points at the attacker's origin.
/// </para>
/// <para>
/// The configured <see cref="AppOptions.PublicBaseUrl"/> always wins. Without it, the origin of the
/// current request is used only when the operator named that exact hostname in <c>AllowedHosts</c>,
/// because then the host is one the operator wrote down rather than one the caller supplied. Otherwise
/// nothing is produced, and actions that depend on such a link cannot complete.
/// </para>
/// </remarks>
public interface IPublicUrlProvider
{
  /// <summary>
  /// Whether this server has an origin it can safely put in front of a user. When false, any action
  /// that depends on emailing such a link cannot complete and should say so rather than half-succeed.
  /// </summary>
  bool HasTrustworthyOrigin { get; }

  /// <summary>
  /// Builds an absolute URL for a path relative to the application root.
  /// </summary>
  /// <param name="relativePath">The path relative to the application root, e.g. "Account/ResetPassword".</param>
  /// <returns>The absolute URL, or <see langword="null"/> when no trustworthy origin exists.</returns>
  string? TryGetAbsoluteUrl(string relativePath);

  /// <summary>
  /// Builds an absolute URL for a path relative to the application root, with query parameters appended.
  /// </summary>
  /// <param name="relativePath">The path relative to the application root, e.g. "Account/ConfirmEmail".</param>
  /// <param name="queryParameters">The query parameters to append.</param>
  /// <returns>The absolute URL, or <see langword="null"/> when no trustworthy origin exists.</returns>
  string? TryGetAbsoluteUrl(string relativePath, IReadOnlyDictionary<string, string?> queryParameters);

  /// <summary>
  /// The absolute base URL to build outbound links from, without a trailing slash.
  /// </summary>
  /// <returns>
  /// The configured <see cref="AppOptions.PublicBaseUrl"/>, or the current request's origin when
  /// <c>AllowedHosts</c> names that hostname outright. <see langword="null"/> when no trustworthy origin
  /// exists, or when a configured value is malformed or is not an origin.
  /// </returns>
  string? TryGetBaseUrl();
}

/// <summary>
/// Default <see cref="IPublicUrlProvider"/>. Singleton so that it serves scoped Razor components, MVC
/// controllers, and the singleton email sender alike.
/// </summary>
public sealed class PublicUrlProvider(
  IOptionsMonitor<AppOptions> appOptions,
  IOptionsMonitor<HostFilteringOptions> hostFilteringOptions,
  IHttpContextAccessor httpContextAccessor,
  ILogger<PublicUrlProvider> logger) : IPublicUrlProvider
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IOptionsMonitor<HostFilteringOptions> _hostFilteringOptions = hostFilteringOptions;
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
  private readonly ILogger<PublicUrlProvider> _logger = logger;

  // Each guards a standing misconfiguration rather than a per-email event, so its message is reported
  // once instead of on every link this server declines to build.
  private int _configuredBaseUrlRejectedReported;

  private int _originNotTrustedReported;

  public bool HasTrustworthyOrigin => TryGetBaseUrl() is not null;

  public string? TryGetAbsoluteUrl(string relativePath)
  {
    var baseUrl = TryGetBaseUrl();
    return baseUrl is null ? null : Combine(baseUrl, relativePath);
  }

  public string? TryGetAbsoluteUrl(string relativePath, IReadOnlyDictionary<string, string?> queryParameters)
  {
    var url = TryGetAbsoluteUrl(relativePath);
    return url is null ? null : QueryHelpers.AddQueryString(url, queryParameters);
  }

  public string? TryGetBaseUrl()
  {
    var configuredBaseUrl = _appOptions.CurrentValue.PublicBaseUrl;
    if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
    {
      return TryNormalizeConfiguredBaseUrl(configuredBaseUrl);
    }

    if (_httpContextAccessor.HttpContext?.Request is not { } request)
    {
      return null;
    }

    if (!RequestHostIsNamedLiterally(request.Host))
    {
      EnsureReportedOriginNotTrusted();
      return null;
    }

    return $"{request.Scheme}://{request.Host}";
  }

  /// <summary>
  /// Whether <paramref name="allowedHosts"/> names at least one hostname literally, in the
  /// semicolon-separated form that host filtering is set up from configuration.
  /// </summary>
  /// <remarks>
  /// A wildcard is not a name. <c>*</c> accepts any host and <c>*.t.local</c> leaves its leftmost labels
  /// to whoever sends the request, so neither identifies this server.
  /// </remarks>
  internal static bool NamesAnyLiteralHost(string? allowedHosts)
  {
    if (string.IsNullOrWhiteSpace(allowedHosts))
    {
      return false;
    }

    var entries = allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return entries.Any(entry => !entry.Contains('*', StringComparison.Ordinal));
  }

  private static string Combine(string baseUrl, string relativePath)
  {
    return $"{baseUrl}/{relativePath.TrimStart('/')}";
  }

  /// <summary>
  /// The host part of <paramref name="host"/> in URI form, without its port. <see langword="null"/> when
  /// there is no host at all.
  /// </summary>
  private static string? ToUriComponentHost(HostString host)
  {
    if (host.Host is not { Length: > 0 } value)
    {
      return null;
    }

    return new HostString(value).ToUriComponent();
  }

  private void EnsureReportedBaseUrlRejected(string configuredBaseUrl)
  {
    if (Interlocked.Exchange(ref _configuredBaseUrlRejectedReported, 1) == 0)
    {
      _logger.LogError(
        "AppOptions:PublicBaseUrl '{PublicBaseUrl}' is not an absolute http(s) origin. Outbound links are " +
        "being omitted until it is corrected.",
        configuredBaseUrl);
    }
  }

  private void EnsureReportedOriginNotTrusted()
  {
    if (Interlocked.Exchange(ref _originNotTrustedReported, 1) == 0)
    {
      _logger.LogWarning(
        "No trustworthy origin is available for the links this server emails out. Set " +
        "AppOptions:PublicBaseUrl to this server's public URL, or name its hostname in AllowedHosts. " +
        "Links are being omitted until one of those is done.");
    }
  }

  /// <summary>
  /// Whether the host of an arriving request is one the operator named outright in <c>AllowedHosts</c>,
  /// which is what makes it safe to echo back into a link.
  /// </summary>
  /// <remarks>
  /// This is deliberately stricter than host filtering itself. Filtering is satisfied by a subdomain
  /// wildcard such as <c>*.t.local</c>, and by the top-level forms that switch it off, both of which leave
  /// the arriving host partly chosen by whoever sent the request.
  /// </remarks>
  private bool RequestHostIsNamedLiterally(HostString requestHost)
  {
    var allowedHosts = _hostFilteringOptions.CurrentValue.AllowedHosts;
    if (allowedHosts is not { Count: > 0 })
    {
      return false;
    }

    var request = ToUriComponentHost(requestHost);
    if (request is null)
    {
      return false;
    }

    foreach (var entry in allowedHosts)
    {
      if (string.Equals(request, ToUriComponentHost(new HostString(entry)), StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  private string? TryNormalizeConfiguredBaseUrl(string configuredBaseUrl)
  {
    var trimmed = configuredBaseUrl.TrimEnd('/');

    // This app is served from the site root, so a configured path would produce broken links.
    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
        string.IsNullOrEmpty(uri.Host) ||
        uri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment) ||
        !string.IsNullOrEmpty(uri.UserInfo))
    {
      EnsureReportedBaseUrlRejected(configuredBaseUrl);
      return null;
    }

    return trimmed;
  }
}

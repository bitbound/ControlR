using ControlR.Web.Server.Options;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

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
/// current request is used only when <c>AllowedHosts</c> pins the hostnames this server answers to,
/// because then the host is one the operator chose rather than the caller. Otherwise nothing is
/// produced, and actions that depend on such a link cannot complete.
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
  /// <c>AllowedHosts</c> pins it. <see langword="null"/> when no trustworthy origin exists, or when a
  /// configured one is malformed.
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

  // Both counters below guard a standing misconfiguration rather than a per-email event, so each
  // message is reported once instead of on every link this server declines to build.
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

    if (!RequestHostIsPinned())
    {
      ReportOnce(ref _originNotTrustedReported,
        logger => logger.LogWarning(
          "No trustworthy origin is available for the links this server emails out. Set " +
          "AppOptions:PublicBaseUrl to this server's public URL, or pin AllowedHosts to its hostnames. " +
          "Links are being omitted until one of those is done."));
      return null;
    }

    if (_httpContextAccessor.HttpContext?.Request is not { } request)
    {
      return null;
    }

    return $"{request.Scheme}://{request.Host}";
  }

  private static string Combine(string baseUrl, string relativePath)
  {
    return $"{baseUrl}/{relativePath.TrimStart('/')}";
  }

  private void ReportOnce(ref int flag, Action<ILogger> report)
  {
    if (Interlocked.Exchange(ref flag, 1) == 0)
    {
      report(_logger);
    }
  }

  /// <summary>
  /// Whether <c>AllowedHosts</c> restricts this server to operator-chosen hostnames, which is what makes
  /// the host of an arriving request safe to echo back into a link.
  /// </summary>
  private bool RequestHostIsPinned()
  {
    var allowedHosts = _hostFilteringOptions.CurrentValue.AllowedHosts;
    if (allowedHosts is not { Count: > 0 })
    {
      return false;
    }

    foreach (var entry in allowedHosts)
    {
      // Mirrors the wildcards that switch host filtering off, so this check cannot drift from what the
      // framework actually enforces.
      var host = new HostString(entry).ToUriComponent();
      if (host is "*" or "[::]" or "0.0.0.0")
      {
        return false;
      }
    }

    return true;
  }

  private string? TryNormalizeConfiguredBaseUrl(string configuredBaseUrl)
  {
    var trimmed = configuredBaseUrl.TrimEnd('/');

    // Only a plain http(s) origin is usable as a link base. Anything else is a misconfiguration, and
    // guessing would silently rewrite where every account email points.
    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
        string.IsNullOrEmpty(uri.Host))
    {
      ReportOnce(ref _configuredBaseUrlRejectedReported,
        logger => logger.LogError(
          "AppOptions:PublicBaseUrl '{PublicBaseUrl}' is not an absolute http(s) URL. Outbound links " +
          "are being omitted until it is corrected.",
          configuredBaseUrl));
      return null;
    }

    return trimmed;
  }
}

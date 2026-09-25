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
/// The origin comes only from the configured <see cref="AppOptions.PublicBaseUrl"/>, never from the
/// request. Without a valid configured value nothing is produced, and actions that depend on such a
/// link cannot complete.
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
  /// The configured <see cref="AppOptions.PublicBaseUrl"/>. <see langword="null"/> when it is not set,
  /// is malformed, or is not an origin.
  /// </returns>
  string? TryGetBaseUrl();
}

/// <summary>
/// Default <see cref="IPublicUrlProvider"/>. Singleton so that it serves scoped Razor components, MVC
/// controllers, and the singleton email sender alike.
/// </summary>
public sealed class PublicUrlProvider(
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<PublicUrlProvider> logger) : IPublicUrlProvider
{
  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly ILogger<PublicUrlProvider> _logger = logger;

  // Guards a standing misconfiguration rather than a per-email event, so its message is reported once
  // instead of on every link this server declines to build.
  private int _configuredBaseUrlRejectedReported;

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
    if (string.IsNullOrWhiteSpace(configuredBaseUrl))
    {
      return null;
    }

    return TryNormalizeConfiguredBaseUrl(configuredBaseUrl);
  }

  private static string Combine(string baseUrl, string relativePath)
  {
    return $"{baseUrl}/{relativePath.TrimStart('/')}";
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

  private string? TryNormalizeConfiguredBaseUrl(string configuredBaseUrl)
  {
    var trimmed = configuredBaseUrl.Trim().TrimEnd('/');

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

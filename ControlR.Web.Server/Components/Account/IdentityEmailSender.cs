using System.Net;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ControlR.Web.Server.Components.Account;

/// <summary>
/// Sends the emails that <c>MapIdentityApi</c> generates, rebuilding each link's origin from the
/// configured public base URL first.
/// </summary>
/// <remarks>
/// <para>
/// The framework builds its confirmation links with its own <c>LinkGenerator</c>, resolving them by
/// endpoint name against the arriving request's scheme and host. Those are caller-controlled, so a
/// forged <c>Host</c> aims a genuine token-bearing link at an attacker's origin. This type is the single
/// seam every one of those emails passes through, so rewriting here closes the whole class of endpoints
/// at once instead of guarding one path at a time.
/// </para>
/// <para>
/// Links built by ControlR's own pages already come from <see cref="IPublicUrlProvider"/> and arrive
/// with the correct origin. Rewriting them is a no-op.
/// </para>
/// </remarks>
internal sealed class IdentityEmailSender(
  IEmailSender emailSender,
  ILogger<IdentityEmailSender> logger,
  IPublicUrlProvider publicUrlProvider) : IEmailSender<AppUser>
{
  private readonly IEmailSender _emailSender = emailSender;
  private readonly ILogger<IdentityEmailSender> _logger = logger;
  private readonly IPublicUrlProvider _publicUrlProvider = publicUrlProvider;

  public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
  {
    if (!TryRewriteOrigin(email, confirmationLink, out var rewrittenLink))
    {
      return Task.CompletedTask;
    }

    return _emailSender.SendEmailAsync(
      email,
      "ControlR Account Confirmation",
      $"Please confirm your ControlR account by following this link: <a href='{rewrittenLink}'>{rewrittenLink}</a>.");
  }

  public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
  {
    // A bare code carries no origin, so there is nothing to rewrite.
    return _emailSender.SendEmailAsync(
      email,
      "ControlR Password Reset",
      $"Please reset your ControlR password using the following code: {resetCode}");
  }

  public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
  {
    if (!TryRewriteOrigin(email, resetLink, out var rewrittenLink))
    {
      return Task.CompletedTask;
    }

    return _emailSender.SendEmailAsync(
      email,
      "ControlR Password Reset",
      $"Please reset your ControlR password by following this link: <a href='{rewrittenLink}'>{rewrittenLink}</a>.");
  }

  /// <summary>
  /// Replaces a link's origin with the configured one, keeping its path and query.
  /// </summary>
  /// <param name="recipient">The address the message would go to. Used only in log messages.</param>
  /// <param name="encodedLink">The link as supplied, HTML-encoded by the caller.</param>
  /// <param name="rewrittenLink">
  /// The HTML-encoded link to send, or the original value when it carries no origin to replace.
  /// </param>
  /// <returns>
  /// <see langword="true"/> when the message may be sent. <see langword="false"/> when the link needs an
  /// origin this server cannot supply, in which case nothing is sent.
  /// </returns>
  private bool TryRewriteOrigin(string recipient, string encodedLink, out string rewrittenLink)
  {
    rewrittenLink = encodedLink;

    var decoded = WebUtility.HtmlDecode(encodedLink);
    if (!Uri.TryCreate(decoded, UriKind.Absolute, out var suppliedUri))
    {
      // A relative link stays relative for the same caller, which is safe.
      return true;
    }

    var baseUrl = _publicUrlProvider.TryGetBaseUrl();
    if (baseUrl is null)
    {
      _logger.LogError(
        "Refusing to email a link to {Recipient} because AppOptions:PublicBaseUrl is not set to a usable " +
        "origin. The link would otherwise take its origin from the request that asked for it.",
        recipient);
      return false;
    }

    var configuredOrigin = new Uri(baseUrl, UriKind.Absolute);
    if (suppliedUri.Scheme == configuredOrigin.Scheme &&
        suppliedUri.Host == configuredOrigin.Host &&
        suppliedUri.Port == configuredOrigin.Port)
    {
      return true;
    }

    // Quiet if it goes wrong, so the mismatch is visible in support logs rather than only in a
    // misdirected email.
    _logger.LogWarning(
      "Rewriting the origin of a link emailed to {Recipient} from {SuppliedOrigin} to the configured " +
      "{ConfiguredOrigin}.",
      recipient,
      suppliedUri.GetLeftPart(UriPartial.Authority),
      baseUrl);

    var rebuilt = $"{baseUrl}{suppliedUri.PathAndQuery}";
    rewrittenLink = HtmlEncoder.Default.Encode(rebuilt);
    return true;
  }
}
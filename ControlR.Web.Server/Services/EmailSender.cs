using ControlR.Web.Server.Primitives;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using MimeKit.Text;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services;

public interface IControlrEmailSender
{
  /// <summary>
  /// Sends an email, reporting which kind of problem stopped it so the caller can answer with the
  /// matching problem document rather than guessing: <see cref="HttpResultErrorCode.Conflict"/> when
  /// sending is switched off, <see cref="HttpResultErrorCode.InternalServerError"/> when the SMTP
  /// settings are missing, and <see cref="HttpResultErrorCode.ServiceUnavailable"/> when the SMTP
  /// server refused the message.
  /// </summary>
  Task<HttpResult> SendEmailWithResult(string email, string subject, string htmlMessage);
}

public class EmailSender(
  IWebHostEnvironment webHostEnvironment,
  IOptionsMonitor<AppOptions> appOptions,
  IPublicUrlProvider publicUrlProvider,
  ILogger<EmailSender> logger) : IControlrEmailSender, IEmailSender
{

  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly ILogger<EmailSender> _logger = logger;
  private readonly IPublicUrlProvider _publicUrlProvider = publicUrlProvider;
  private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;


  public async Task SendEmailAsync(string email, string subject, string htmlMessage)
  {
    try
    {
      var currentOptions = _appOptions.CurrentValue;

      if (currentOptions.DisableEmailSending)
      {
        _logger.LogInformation(
          "Email sending is disabled.  Email to \"{ToEmail}\" with subject \"{Subject}\" will not be sent.",
          email,
          subject);

        return;
      }

      if (string.IsNullOrWhiteSpace(currentOptions.SmtpDisplayName) ||
          string.IsNullOrWhiteSpace(currentOptions.SmtpEmail) ||
          string.IsNullOrWhiteSpace(currentOptions.SmtpHost))
      {
        _logger.LogCritical("SMTP options are not properly configured.  Unable to send email.");
        throw new InvalidOperationException("SMTP options are not properly configured.  Unable to send email.");
      }

      var message = new MimeMessage();
      message.From.Add(new MailboxAddress(currentOptions.SmtpDisplayName, currentOptions.SmtpEmail));
      message.To.Add(MailboxAddress.Parse(email));
      message.ReplyTo.Add(MailboxAddress.Parse(currentOptions.SmtpEmail));
      message.Subject = subject;

      if (TryGetLogoHtml(out var logoHtml))
      {
        message.Body = new TextPart(TextFormat.Html)
        {
          Text = $"{logoHtml}<br/>{htmlMessage}"
        };
      }
      else
      {
        var builder = new BodyBuilder
        {
          HtmlBody =
            $"<img src='cid:logo' alt='Company Logo' width='256' /> <br /> {htmlMessage}"
        };
        var logoFile = _webHostEnvironment.WebRootFileProvider.GetFileInfo("images/company-logo.png");
        if (logoFile.Exists)
        {
          var logo = builder.LinkedResources.Add(logoFile.PhysicalPath!);
          logo.ContentId = "logo";
        }
        message.Body = builder.ToMessageBody();
      }

      using var client = new SmtpClient();

      if (!string.IsNullOrWhiteSpace(currentOptions.SmtpLocalDomain))
      {
        client.LocalDomain = currentOptions.SmtpLocalDomain;
      }

      client.CheckCertificateRevocation = currentOptions.SmtpCheckCertificateRevocation;

      await client.ConnectAsync(currentOptions.SmtpHost, currentOptions.SmtpPort);

      if (!string.IsNullOrWhiteSpace(currentOptions.SmtpUserName) &&
          !string.IsNullOrWhiteSpace(currentOptions.SmtpPassword))
      {
        await client.AuthenticateAsync(currentOptions.SmtpUserName, currentOptions.SmtpPassword);
      }
      await client.SendAsync(message);
      await client.DisconnectAsync(true);

      _logger.LogInformation("Email successfully sent to {ToEmail}.  Subject: \"{Subject}\".", email, subject);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending email.");
      throw;
    }
  }

  public async Task<HttpResult> SendEmailWithResult(string email, string subject, string htmlMessage)
  {
    try
    {
      var currentOptions = _appOptions.CurrentValue;

      if (currentOptions.DisableEmailSending)
      {
        _logger.LogInformation(
          "Email sending is disabled.  Email to \"{ToEmail}\" with subject \"{Subject}\" will not be sent.",
          email,
          subject);

        return HttpResult.Fail(HttpResultErrorCode.Conflict, "Email sending is disabled.");
      }

      if (string.IsNullOrWhiteSpace(currentOptions.SmtpDisplayName) ||
          string.IsNullOrWhiteSpace(currentOptions.SmtpEmail) ||
          string.IsNullOrWhiteSpace(currentOptions.SmtpHost))
      {
        _logger.LogCritical("SMTP options are not properly configured.  Unable to send email.");
        return HttpResult.Fail(
          HttpResultErrorCode.InternalServerError,
          "SMTP options are not properly configured.  Unable to send email.");
      }

      var message = new MimeMessage();
      message.From.Add(new MailboxAddress(currentOptions.SmtpDisplayName, currentOptions.SmtpEmail));
      message.To.Add(MailboxAddress.Parse(email));
      message.ReplyTo.Add(MailboxAddress.Parse(currentOptions.SmtpEmail));
      message.Subject = subject;

      if (TryGetLogoHtml(out var logoHtml))
      {
        message.Body = new TextPart(TextFormat.Html)
        {
          Text = $"{logoHtml}<br/>{htmlMessage}"
        };
      }
      else
      {
        var builder = new BodyBuilder
        {
          HtmlBody =
            $"<img src='cid:logo' alt='Company Logo' width='256' /> <br /> {htmlMessage}"
        };
        var logoFile = _webHostEnvironment.WebRootFileProvider.GetFileInfo("images/company-logo.png");
        if (logoFile.Exists)
        {
          var logo = builder.LinkedResources.Add(logoFile.PhysicalPath!);
          logo.ContentId = "logo";
        }
        message.Body = builder.ToMessageBody();
      }

      using var client = new SmtpClient();

      if (!string.IsNullOrWhiteSpace(currentOptions.SmtpLocalDomain))
      {
        client.LocalDomain = currentOptions.SmtpLocalDomain;
      }

      client.CheckCertificateRevocation = currentOptions.SmtpCheckCertificateRevocation;

      await client.ConnectAsync(currentOptions.SmtpHost, currentOptions.SmtpPort);

      if (!string.IsNullOrWhiteSpace(currentOptions.SmtpUserName) &&
          !string.IsNullOrWhiteSpace(currentOptions.SmtpPassword))
      {
        await client.AuthenticateAsync(currentOptions.SmtpUserName, currentOptions.SmtpPassword);
      }
      await client.SendAsync(message);
      await client.DisconnectAsync(true);

      _logger.LogInformation("Email successfully sent to {ToEmail}.  Subject: \"{Subject}\".", email, subject);
      return HttpResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending email.");
      return HttpResult.Fail(ex, HttpResultErrorCode.ServiceUnavailable, "Error while sending email.");
    }
  }

  private bool TryGetLogoHtml([NotNullWhen(true)] out string? logoHtml)
  {
    logoHtml = null;

    // The logo rides inside an email this server delivered, so its origin comes only from the
    // configured public URL, never from the request that happened to be in flight.
    if (_publicUrlProvider.TryGetBaseUrl() is not { } baseUrl ||
        !Uri.TryCreate($"{baseUrl}/images/company-logo.png", UriKind.Absolute, out var imageUrl))
    {
      return false;
    }

    // A loopback origin only exists in a local dev setup, where nobody receiving the mail can reach it.
    if (imageUrl.IsLoopback)
    {
      return false;
    }

    logoHtml = $"""
      <img 
        src="{imageUrl}" 
        alt="Company Logo"
        width="256" />
    """;
    return true;
  }
}

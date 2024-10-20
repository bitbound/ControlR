using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit.Text;
using MimeKit;
using MailKit.Net.Smtp;

namespace ControlR.Web.Server.Services;

public class EmailSender(
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<EmailSender> logger) : IEmailSender
{
  private readonly AppOptions _appOptions = appOptions.CurrentValue;
  private readonly ILogger<EmailSender> _logger = logger;

  public async Task SendEmailAsync(string email, string subject, string htmlMessage)
  {
    try
    {
      if (!ValidateOptions())
      {
        _logger.LogCritical("SMTP options are not properly configured.  Unable to send email.");
        return;
      }

      var message = new MimeMessage();
      message.From.Add(new MailboxAddress(_appOptions.SmtpDisplayName, _appOptions.SmtpEmail));
      message.To.Add(MailboxAddress.Parse(email));
      message.ReplyTo.Add(MailboxAddress.Parse(_appOptions.SmtpEmail));
      message.Subject = subject;
      message.Body = new TextPart(TextFormat.Html)
      {
        Text = htmlMessage
      };

      using var client = new SmtpClient();

      if (!string.IsNullOrWhiteSpace(_appOptions.SmtpLocalDomain))
      {
        client.LocalDomain = _appOptions.SmtpLocalDomain;
      }

      client.CheckCertificateRevocation = _appOptions.SmtpCheckCertificateRevocation;

      await client.ConnectAsync(_appOptions.SmtpHost, _appOptions.SmtpPort);

      if (!string.IsNullOrWhiteSpace(_appOptions.SmtpUserName) &&
          !string.IsNullOrWhiteSpace(_appOptions.SmtpPassword))
      {
        await client.AuthenticateAsync(_appOptions.SmtpUserName, _appOptions.SmtpPassword);
      }
      await client.SendAsync(message);
      await client.DisconnectAsync(true);

      _logger.LogInformation("Email successfully sent to {toEmail}.  Subject: \"{subject}\".", email, subject);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while sending email.");
      throw;
    }
  }

  private bool ValidateOptions()
  {
    if (string.IsNullOrWhiteSpace(_appOptions.SmtpDisplayName) ||
        string.IsNullOrWhiteSpace(_appOptions.SmtpEmail) ||
        string.IsNullOrWhiteSpace(_appOptions.SmtpHost))
    {
      return false;
    }
    return true;
  }
}

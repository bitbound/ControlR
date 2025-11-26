using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using MimeKit.Text;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services;

public class EmailSender(
  IWebHostEnvironment webHostEnvironment,
  IHttpContextAccessor httpContextAccessor,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<EmailSender> logger) : IEmailSender
{

  private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
  private readonly ILogger<EmailSender> _logger = logger;
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

      if (!ValidateOptions())
      {
        _logger.LogCritical("SMTP options are not properly configured.  Unable to send email.");
        return;
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



  private bool TryGetLogoHtml([NotNullWhen(true)]out string? logoHtml)
  {
    if (_httpContextAccessor.HttpContext?.Request is not { } request)
    {
      logoHtml = null;
      return false;
    }

    if (request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
      logoHtml = null;
      return false;
    }

    var imageUrl = new Uri(request.ToOrigin(), "/images/company-logo.png");

    logoHtml = $"""
      <img 
        src="{imageUrl}" 
        alt="Company Logo"
        width="256" />
    """;
    return true;
  }

  private bool ValidateOptions()
  {
    if (string.IsNullOrWhiteSpace(_appOptions.CurrentValue.SmtpDisplayName) ||
        string.IsNullOrWhiteSpace(_appOptions.CurrentValue.SmtpEmail) ||
        string.IsNullOrWhiteSpace(_appOptions.CurrentValue.SmtpHost))
    {
      return false;
    }
    return true;
  }
}
